using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Toast.Web.Data;

/// <summary>
/// Chỉ cho file SQLite khi chạy thử (ConnectionStrings:ToastSqlite). EnsureCreated không thêm bảng/cột vào file đã có,
/// nên lưu dấu cấu trúc bảng ra file &lt;db&gt;.schema. Cấu trúc đổi thì: đổi tên file cũ thành &lt;db&gt;.bak-&lt;thời gian&gt;,
/// tạo file mới, rồi chép lại dữ liệu cũ (các bảng / cột còn có trong cấu trúc mới). Giữ 5 bản sao lưu gần nhất.
/// </summary>
public static class DevSqlite
{
    const int KeepBackups = 5;

    // Bảng / cột đã đổi tên: tên mới -> (tên bảng cũ, cột mới -> các tên cũ, lấy tên đầu tiên có trong file cũ), để chép dữ liệu từ file DB cũ sang
    static readonly Dictionary<string, (string Table, Dictionary<string, string[]> Columns)> Renamed = new()
    {
        ["Toast_Package"] = ("Toast_Box", new() { ["PackageNo"] = ["BoxNo"], ["ProductId"] = ["ViindooProductId"], ["DefaultCode"] = ["PartNumber"] }),
        ["Toast_Product"] = ("Toast_Product", new() { ["ProductId"] = ["ViindooProductId"], ["DefaultCode"] = ["PartNumber"] }),
        // 005 đổi PartNumber -> Name, 006 đổi lại, 007 đổi thành DefaultCode
        ["Toast_LabelTemplate"] = ("Toast_LabelTemplate", new() { ["DefaultCode"] = ["PartNumber", "Name"] })
    };

    public static void EnsureSchema(IServiceProvider services, string connectionString)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>().Database;
        var log = scope.ServiceProvider.GetRequiredService<ILogger<ToastDbContext>>();

        string dbFile = new SqliteConnectionStringBuilder(connectionString).DataSource;
        string schemaFile = dbFile + ".schema";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.GenerateCreateScript())));
        string? oldHash = File.Exists(schemaFile) ? File.ReadAllText(schemaFile).Trim() : null;

        // File cũ chưa có dấu .schema (tạo trước khi có cơ chế này) cũng coi như khác cấu trúc
        if (oldHash != hash && HasTables(db))
        {
            string backup = Backup(db, dbFile);
            db.EnsureCreated();
            CreateSharedTables(db);
            var copied = CopyData(db, backup, log);
            FillWoGroup(db);
            log.LogWarning("Cấu trúc bảng đã thay đổi: đã tạo lại DB chạy thử, sao lưu file cũ ở {Backup}, chép lại dữ liệu: {Copied}.",
                backup, copied.Count == 0 ? "(không có)" : string.Join(", ", copied));
            DeleteOldBackups(dbFile);
        }
        db.EnsureCreated();
        CreateSharedTables(db);
        File.WriteAllText(schemaFile, hash);
    }

    // Phiên / box chép từ file cũ chưa có nhóm lệnh: nhóm = tên lệnh bỏ đuôi backorder (giống 011_SessionGroup.sql)
    static void FillWoGroup(DatabaseFacade db)
    {
        foreach (string table in new[] { "Toast_Session", "Toast_Package" })
            db.ExecuteSqlRaw($"UPDATE \"{table}\" SET \"WoGroup\" = CASE WHEN instr(\"WoName\", '-') > 0 " +
                "THEN substr(\"WoName\", 1, instr(\"WoName\", '-') - 1) ELSE \"WoName\" END WHERE \"WoGroup\" = ''");
    }

    // Bảng có sẵn của SVN-Portal mà Toast ghi vào (migration không tạo): chạy thử bằng SQLite thì tạo bảng giả cùng cột
    static void CreateSharedTables(DatabaseFacade db) => db.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "SVN_ProductionInputLogs" (
            "id" INTEGER PRIMARY KEY AUTOINCREMENT, "level" INTEGER, "product_id" INTEGER, "product_qty" TEXT,
            "date_finished" TEXT, "product_type" TEXT, "serial_code" TEXT, "component_list" TEXT, "state" TEXT,
            "API_function" TEXT, "API_parameters" TEXT, "status" TEXT, "wo_code" TEXT, "master_wo_code" TEXT,
            "total_qty" TEXT, "remain_qty" TEXT, "consumed_wo_code" TEXT)
        """);

    // Ghi hết WAL vào file chính, đóng kết nối rồi đổi tên file DB (kèm -wal / -shm nếu còn)
    static string Backup(DatabaseFacade db, string dbFile)
    {
        db.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
        db.CloseConnection();
        SqliteConnection.ClearAllPools();

        string backup = $"{dbFile}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(dbFile, backup);
        foreach (string suffix in new[] { "-wal", "-shm" })
            if (File.Exists(dbFile + suffix)) File.Delete(dbFile + suffix);
        return backup;
    }

    // Chép dữ liệu từ file sao lưu sang DB mới: bảng nào có ở cả 2 (hoặc đã đổi tên, xem Renamed) thì chép các cột chung (giữ nguyên Id).
    // Cột đổi từ chữ sang số: giá trị không phải số thành NULL (VD Toast_Box.PrinterId 'DEV-1' cũ).
    // Cột mới bắt buộc mà không có giá trị mặc định thì không chép được bảng đó (dữ liệu vẫn còn trong file sao lưu).
    static List<string> CopyData(DatabaseFacade db, string backup, ILogger log)
    {
        var copied = new List<string>();
        var conn = (SqliteConnection)db.GetDbConnection();
        conn.Open();
        try
        {
            // Tắt khoá ngoại khi chép để không phụ thuộc thứ tự bảng (VD Toast_Package -> Toast_Printer)
            Exec(conn, "PRAGMA foreign_keys = OFF");
            Exec(conn, "ATTACH DATABASE $path AS old", ("$path", backup));
            var tables = Strings(conn, "SELECT name FROM main.sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");

            foreach (string table in tables)
            {
                string source = table;
                var oldTypes = Columns(conn, table, "old");
                var renamedColumns = new Dictionary<string, string[]>();
                if (Renamed.TryGetValue(table, out var renamed))
                {
                    renamedColumns = renamed.Columns;
                    if (oldTypes.Count == 0)
                    {
                        source = renamed.Table;
                        oldTypes = Columns(conn, source, "old");
                    }
                }
                // Cột còn tên cũ trong file cũ thì lấy theo tên cũ
                string OldName(string column) => oldTypes.ContainsKey(column) ? column
                    : renamedColumns.GetValueOrDefault(column, []).FirstOrDefault(oldTypes.ContainsKey) ?? column;

                var columns = Columns(conn, table, "main").Where(c => oldTypes.ContainsKey(OldName(c.Key))).ToList();
                if (columns.Count == 0) continue;

                string list = string.Join(", ", columns.Select(c => $"\"{c.Key}\""));
                string values = string.Join(", ", columns.Select(c =>
                {
                    string old = OldName(c.Key);
                    return c.Value == "INTEGER" && oldTypes[old] != "INTEGER"
                        ? $"CASE WHEN CAST(\"{old}\" AS INTEGER) || '' = \"{old}\" THEN CAST(\"{old}\" AS INTEGER) END"
                        : $"\"{old}\"";
                }));
                try
                {
                    int rows = Exec(conn, $"INSERT INTO main.\"{table}\" ({list}) SELECT {values} FROM old.\"{source}\"");
                    if (rows > 0) copied.Add($"{table} ({rows} dòng)");
                }
                catch (SqliteException ex)
                {
                    log.LogWarning("Không chép được bảng {Table} sang DB mới: {Error}. Dữ liệu vẫn còn trong file sao lưu.", table, ex.Message);
                }
            }
            Exec(conn, "DETACH DATABASE old");
            Exec(conn, "PRAGMA foreign_keys = ON");
        }
        finally
        {
            conn.Close();
        }
        return copied;
    }

    static void DeleteOldBackups(string dbFile)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(dbFile))!;
        foreach (string old in Directory.GetFiles(dir, Path.GetFileName(dbFile) + ".bak-*").OrderDescending().Skip(KeepBackups))
            File.Delete(old);
    }

    static int Exec(SqliteConnection conn, string sql, params (string name, object value)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteNonQuery();
    }

    // Tên cột -> kiểu (INTEGER, TEXT...) của 1 bảng trong schema main / old
    static Dictionary<string, string> Columns(SqliteConnection conn, string table, string schema)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT name, upper(type) FROM pragma_table_info('{table}', '{schema}')";
        using var reader = cmd.ExecuteReader();
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) columns[reader.GetString(0)] = reader.GetString(1);
        return columns;
    }

    static List<string> Strings(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var list = new List<string>();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    // File DB đã có bảng nào chưa (file mới / trống thì không cần xoá)
    static bool HasTables(DatabaseFacade db)
    {
        var conn = db.GetDbConnection();
        conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
        finally
        {
            conn.Close();
        }
    }
}
