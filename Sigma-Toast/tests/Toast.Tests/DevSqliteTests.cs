using Microsoft.Data.Sqlite;

namespace Toast.Tests;

/// <summary>File SQLite chạy thử tạo từ bản cũ (thiếu cột): tạo lại khi khởi động, sao lưu file cũ và chép lại dữ liệu.</summary>
public class DevSqliteTests
{
    [Fact]
    public async Task Old_dev_db_missing_columns_is_recreated()
    {
        using var factory = new ToastFactory();
        // Giống toast-dev.db cũ: bảng Toast_LabelTemplate chưa có Dpi / Variables / IsLocked, Toast_Box.PrinterId còn là chữ
        // (ID của SVN_Printer_Info), chưa có file .schema
        using (var conn = new SqliteConnection($"Data Source={factory.DbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Toast_LabelTemplate (Id INTEGER PRIMARY KEY, PartNumber TEXT, Type TEXT, Operation TEXT, Size TEXT, ZplData TEXT, CreatedAt TEXT, UpdatedAt TEXT);" +
                "INSERT INTO Toast_LabelTemplate VALUES (7, 'PN-OLD', 'Box', 'Toast', '4x6', '^XA^XZ', '2026-10-30 10:00:00', NULL);" +
                "CREATE TABLE Toast_Box (Id INTEGER PRIMARY KEY, WoName TEXT, ViindooProductId INTEGER, PartNumber TEXT, BoxNo INTEGER, Qty INTEGER, Capacity INTEGER, Status TEXT, PrinterId TEXT, CreatedAt TEXT);" +
                "INSERT INTO Toast_Box VALUES (3, 'NM/MO/OLD', 951, 'PN-OLD', 1, 5, 5, 'Closed', 'DEV-1', '2026-10-30 10:00:00');";
            cmd.ExecuteNonQuery();
        }

        var client = factory.CreateClient();
        var loginPage = await client.GetStringAsync("/toast/admin/account/login");
        var token = System.Text.RegularExpressions.Regex.Match(loginPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = token
        }));

        var json = await client.GetStringAsync("/toast/admin/label-template/api");
        Assert.Contains("\"success\":true", json);
        Assert.True(File.Exists(factory.DbPath + ".schema"));

        // Dữ liệu cũ được chép sang DB mới (cột mới Dpi / Variables / IsLocked lấy giá trị mặc định), file cũ được sao lưu
        Assert.Contains("\"defaultCode\":\"PN-OLD\"", json); // PartNumber cũ -> DefaultCode
        Assert.Contains("\"id\":7", json);
        Assert.Contains("\"dpi\":600", json);
        // Toast_Box cũ chép sang Toast_Package (BoxNo -> PackageNo); PrinterId chữ không khớp máy in nào: thành NULL
        var boxes = await client.GetStringAsync("/toast/admin/package/api");
        Assert.Contains("\"woName\":\"NM/MO/OLD\"", boxes);
        Assert.Contains("\"packageNo\":1", boxes);
        Assert.Contains("\"printerId\":null", boxes);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(factory.DbPath)!, Path.GetFileName(factory.DbPath) + ".bak-*"));
    }
}
