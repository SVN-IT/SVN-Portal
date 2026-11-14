using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Toast.Web.Features.Station;

/// <summary>
/// Log lỗi gửi từ trình duyệt màn công nhân (/toast/api/client-log): máy trạm là máy công nhân, không xem console được thì đọc ở server.
/// Ghi file theo ngày {Toast:ClientLogDir}/client-yyyyMMdd.log (mặc định Windows C:\ProgramData\SigmaToast\logs, máy khác thư mục "logs" của app),
/// mỗi dòng: giờ server | giờ máy trạm | mức | máy | IP | WO | sự kiện | nội dung | dữ liệu JSON. Giữ 30 ngày.
/// </summary>
public sealed class ClientLogService(IWebHostEnvironment env, IOptionsMonitor<StationOptions> options, ILogger<ClientLogService> logger)
{
    const int KeepDays = 30;
    const int MaxField = 2000;
    readonly object fileLock = new();
    DateTime lastCleanup = DateTime.MinValue;

    // Lỗi ghi file gần nhất (VD app pool IIS không có quyền tạo thư mục): màn Log trạm hiện ra để biết vì sao không có log
    public string? LastWriteError { get; private set; }
    public DateTime? LastWriteErrorAt { get; private set; }
    public DateTime? LastWriteAt { get; private set; }

    public string Directory
    {
        get
        {
            // Có Toast:ClientLogDir: tuyệt đối dùng luôn, tương đối tính từ thư mục app (bản build).
            // Không có: Windows = C:\ProgramData\SigmaToast\logs (cùng chỗ khoá Data Protection; app pool tự tạo được, không phải cấp
            // quyền, các bản deploy theo ngày dùng chung), máy khác (dev Linux) = <thư mục app>/logs
            if (options.CurrentValue.ClientLogDir is not { Length: > 0 } configured)
                return OperatingSystem.IsWindows()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SigmaToast", "logs")
                    : Path.Combine(env.ContentRootPath, "logs");
            string dir = configured.Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.IsPathRooted(dir) ? dir : Path.Combine(env.ContentRootPath, dir));
        }
    }

    // true = đã ghi file (hoặc không có gì để ghi). false = ghi lỗi: trạm giữ log lại, gửi lại sau (không mất log)
    public bool Write(ClientLogRequest req, string? ip)
    {
        var entries = (req.Entries ?? []).Take(ClientLogRequest.MaxEntries).ToList();
        if (entries.Count == 0) return true;
        var now = DateTime.Now;
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            sb.Append(now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" | ")
              .Append(Clean(e.Time, 40)).Append(" | ")
              .Append(Clean(e.Level, 10)).Append(" | ")
              .Append(Clean(req.Device, 64)).Append(" | ")
              .Append(Clean(ip, 64)).Append(" | ")
              .Append(Clean(e.Wo, 50)).Append(" | ")
              .Append(Clean(e.Event, 100)).Append(" | ")
              .Append(Clean(e.Message, MaxField)).Append(" | ")
              .Append(Clean(e.Data is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } d ? d.GetRawText() : "", MaxField))
              .AppendLine();
        }

        string dir = Directory;
        try
        {
            lock (fileLock)
            {
                System.IO.Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, $"client-{now:yyyyMMdd}.log"), sb.ToString(), Encoding.UTF8);
                if ((now - lastCleanup).TotalHours >= 12) { lastCleanup = now; Cleanup(dir, now); }
                LastWriteAt = now;
                LastWriteError = null;
            }
        }
        catch (Exception ex)
        {
            // Không ghi được file (thiếu quyền...): ghi log của app (cùng lỗi thì 10 phút 1 lần, trạm gửi lại mỗi lần ping)
            if (ex.Message != LastWriteError || LastWriteErrorAt is not DateTime at || (now - at).TotalMinutes >= 10)
                logger.LogError(ex, "Không ghi được log trạm vào {Dir}", dir);
            LastWriteError = ex.Message;
            LastWriteErrorAt = now;
            return false;
        }
        foreach (var e in entries.Where(e => e.Level == "error"))
            logger.LogWarning("Trạm {Device} ({Ip}) lỗi {Event}: {Message}", req.Device, ip, e.Event, e.Message);
        return true;
    }

    // ===== Đọc (màn admin Log trạm) =====

    // Các ngày có file log, mới nhất trước (yyyyMMdd)
    public List<string> Days()
    {
        string dir = Directory;
        if (!System.IO.Directory.Exists(dir)) return [];
        return System.IO.Directory.GetFiles(dir, "client-*.log")
            .Select(f => Path.GetFileNameWithoutExtension(f)["client-".Length..])
            .Where(IsDay).OrderByDescending(d => d).ToList();
    }

    public static bool IsDay(string? day) => day is { Length: 8 } && day.All(char.IsDigit);

    public string? FilePath(string day)
    {
        if (!IsDay(day)) return null;
        string path = Path.Combine(Directory, $"client-{day}.log");
        return File.Exists(path) ? path : null;
    }

    // `max` dòng cuối của ngày (mới nhất trước); total = tổng số dòng của file
    public (List<ClientLogLine> rows, int total) Read(string day, int max)
    {
        string? path = FilePath(day);
        if (path == null) return ([], 0);
        var last = new Queue<string>();
        int total = 0;
        lock (fileLock)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                total++;
                last.Enqueue(line);
                if (last.Count > max) last.Dequeue();
            }
        }
        int no = total - last.Count;
        var rows = last.Select(l => Parse(l, ++no)).ToList();
        rows.Reverse();
        return (rows, total);
    }

    // giờ server | giờ trạm | mức | máy | IP | WO | sự kiện | nội dung | dữ liệu (các trường đã bỏ dấu |)
    static ClientLogLine Parse(string line, int no)
    {
        var f = line.Split(" | ", 9);
        string Get(int i) => i < f.Length ? f[i].Trim() : "";
        return new ClientLogLine(no, Get(0), Get(1), Get(2), Get(3), Get(4), Get(5), Get(6), Get(7), Get(8));
    }

    static void Cleanup(string dir, DateTime now)
    {
        foreach (var file in System.IO.Directory.GetFiles(dir, "client-*.log"))
            if (File.GetLastWriteTime(file) < now.AddDays(-KeepDays)) File.Delete(file);
    }

    // 1 dòng / log: bỏ xuống dòng, cắt độ dài
    static string Clean(string? s, int max)
    {
        string v = (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        return v.Length > max ? v[..max] + "…" : v;
    }
}

public sealed record ClientLogLine(int No, string ServerTime, string ClientTime, string Level, string Device, string Ip, string Wo,
    string Event, string Message, string Data);

public class ClientLogRequest
{
    public const int MaxEntries = 100;
    public string? Device { get; set; }
    public List<ClientLogEntry>? Entries { get; set; }
}

public class ClientLogEntry
{
    // Giờ máy trạm lúc xảy ra (chuỗi giờ địa phương)
    public string? Time { get; set; }
    // error | warn | info
    public string? Level { get; set; }
    public string? Event { get; set; }
    public string? Message { get; set; }
    public string? Wo { get; set; }
    public JsonElement? Data { get; set; }
}
