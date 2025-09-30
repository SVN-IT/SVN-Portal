using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AutomationService.Models;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AutomationService.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    private string ip = "192.168.2.203";
    private int port = 55443;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {

        SetPower(true);

        // Đổi sang màu đỏ
        SetColor(255, 0, 0);

        // Tăng độ sáng lên 80%
        SetBrightness(80);

        // Tắt đèn sau 3 giây
        System.Threading.Thread.Sleep(3000);
        SetPower(false);

        //var client = new XiaomiCloudClient();
        //bool ok = await client.LoginAsync("datp1044@gmail.com", "Halo_1234");

        //if (ok)
        //{
        //    var devices = await client.GetDeviceList("sg"); // hoặc "cn", "us", "de", "ru", "in"
        //    Console.WriteLine("Devices:");
        //    Console.WriteLine(devices);
        //}

        return View();
    }

    public static string BuildCookieHeader(ICookieJar cookies)
    {
        // lấy tất cả cookie, không lọc
        var list = cookies.AllCookies
            .Select(c => $"{c.Name}={c.Value}");
        return string.Join("; ", list);
    }

    public static async Task<string> GetDeviceList(string cookieHeader, string region = "sg")
    {
        var url = $"https://{region}.api.io.mi.com/app/home/device_list";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        client.DefaultRequestHeaders.Add("User-Agent", "MiHome/6.0.701 (iPhone; iOS 16.0; Scale/3.00)");

        var res = await client.PostAsync(url, new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Error {res.StatusCode}: {body}");
        }

        return body;
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private void SendCommand(string command)
    {
        using (TcpClient client = new TcpClient())
        {
            client.Connect(ip, port);
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(command + "\r\n");
            stream.Write(data, 0, data.Length);
            stream.Close();
            client.Close();
        }
    }

    // Bật / Tắt đèn
    public void SetPower(bool on)
    {
        string state = on ? "on" : "off";
        string cmd = $"{{\"id\":1,\"method\":\"set_power\",\"params\":[\"{state}\",\"smooth\",500]}}";
        SendCommand(cmd);
    }

    // Đổi màu RGB
    public void SetColor(int r, int g, int b)
    {
        int rgb = (r << 16) + (g << 8) + b; // convert sang decimal
        string cmd = $"{{\"id\":1,\"method\":\"set_rgb\",\"params\":[{rgb},\"smooth\",500]}}";
        SendCommand(cmd);
    }

    // Đổi độ sáng
    public void SetBrightness(int brightness) // brightness: 1 - 100
    {
        string cmd = $"{{\"id\":1,\"method\":\"set_bright\",\"params\":[{brightness},\"smooth\",500]}}";
        SendCommand(cmd);
    }
}

public class XiaomiCloudClient
{
    private readonly HttpClient _http;
    private string _userId;
    private string _serviceToken;
    private string _ssecurity;

    public XiaomiCloudClient()
    {
        _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false
        });
    }

    /// <summary>
    /// Login vào Xiaomi Cloud
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password)
    {
        // 1. Lấy _sign
        var loginPage = await _http.GetStringAsync("https://account.xiaomi.com/pass/serviceLogin?sid=xiaomiio");

        // Tìm _sign trong input hidden
        var signMatch = Regex.Match(loginPage, @"name=""_sign"" value=""(?<val>[^""]+)""");
        if (!signMatch.Success)
        {
            Console.WriteLine("❌ Không tìm thấy _sign trong login page");
            return false;
        }
        var _sign = signMatch.Groups["val"].Value;

        // 2. Gửi login
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {"_json", "true"},
            {"_sign", _sign},
            {"sid", "xiaomiio"},
            {"hash", Convert.ToBase64String(Encoding.UTF8.GetBytes(password))}, // password hash tạm
            {"user", username}
        });

        var res = await _http.PostAsync("https://account.xiaomi.com/pass/serviceLoginAuth2", content);
        var body = await res.Content.ReadAsStringAsync();
        body = body.Replace("&&&START&&&", ""); // Xiaomi trả về JSON kèm prefix

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("ssecurity", out var sec))
        {
            _ssecurity = sec.GetString();
            _userId = root.GetProperty("userId").GetString();
        }
        else
        {
            Console.WriteLine("❌ Login thất bại: " + body);
            return false;
        }

        var location = root.GetProperty("location").GetString();

        // 3. Lấy serviceToken từ redirect
        var res2 = await _http.GetAsync(location);
        if (!res2.Headers.Contains("Set-Cookie"))
        {
            Console.WriteLine("❌ Không lấy được serviceToken");
            return false;
        }

        foreach (var cookie in res2.Headers.GetValues("Set-Cookie"))
        {
            if (cookie.StartsWith("serviceToken"))
            {
                _serviceToken = cookie.Split(';')[0].Split('=')[1];
            }
        }

        Console.WriteLine($"✅ Login thành công, userId={_userId}");
        return true;
    }

    /// <summary>
    /// Gọi API Cloud, có ký bằng ssecurity
    /// </summary>
    public async Task<string> CallApiAsync(string path, string data = "{}", string region = "sg")
    {
        if (string.IsNullOrEmpty(_serviceToken) || string.IsNullOrEmpty(_ssecurity))
            throw new InvalidOperationException("Chưa login!");

        string url = $"https://{region}.api.io.mi.com/app{path}";
        string nonce = CreateNonce();
        string signedNonce = SignedNonce(_ssecurity, nonce);
        string signature = GenSignature(path, signedNonce, nonce, data);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {"data", data},
            {"rc4_hash__", ""},
            {"signature", signature},
            {"_nonce", nonce}
        });

        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = body;
        req.Headers.Add("Cookie", $"userId={_userId}; serviceToken={_serviceToken}");
        req.Headers.Add("User-Agent", "Android-7.1.1-1.0.0-ONEPLUS A3010-136-0-MIUI/1.0.0 App/xiaomi.smarthome/6.0.103");

        var res = await _http.SendAsync(req);
        var resBody = await res.Content.ReadAsStringAsync();
        return resBody;
    }

    /// <summary>
    /// Lấy danh sách thiết bị
    /// </summary>
    public Task<string> GetDeviceList(string region = "sg")
    {
        return CallApiAsync("/home/device_list", "{}", region);
    }

    #region Helpers
    private static string CreateNonce()
    {
        var rnd = new byte[12];
        RandomNumberGenerator.Fill(rnd);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var nonce = new byte[16];
        Array.Copy(rnd, nonce, 12);
        Array.Copy(BitConverter.GetBytes(ts), 0, nonce, 12, 4);
        return Convert.ToBase64String(nonce);
    }

    private static string SignedNonce(string ssecurity, string nonce)
    {
        var sec = Convert.FromBase64String(ssecurity);
        var non = Convert.FromBase64String(nonce);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(sec.Concat(non).ToArray());
        return Convert.ToBase64String(hash);
    }

    private static string GenSignature(string path, string signedNonce, string nonce, string data)
    {
        string s = $"{path}&{signedNonce}&{nonce}&{data}";
        using var hmac = new HMACSHA256(Convert.FromBase64String(signedNonce));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToBase64String(sig);
    }
    #endregion
}
