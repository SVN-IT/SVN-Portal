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

        var options = new ChromeOptions();
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--start-maximized");

        using var driver = new ChromeDriver(options);

        // 1. Mở trang login
        driver.Navigate().GoToUrl("https://account.xiaomi.com/pass/serviceLogin?sid=xiaomiio");

        Console.WriteLine("👉 Đăng nhập Xiaomi trên Chrome...");
        Console.WriteLine("👉 Sau khi đăng nhập thành công, nhấn ENTER để tiếp tục.");

        // 2. Lấy toàn bộ cookie
        var cookieHeader = BuildCookieHeader(driver.Manage().Cookies);

        Console.WriteLine("✅ CookieHeader:");
        Console.WriteLine(cookieHeader);

        // 3. Gọi API
        string region = "sg"; // hoặc "cn", "de", "ru"
        var devices = await GetDeviceList(cookieHeader, region);

        Console.WriteLine("✅ Device List:");
        Console.WriteLine(devices);

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

class XiaomiApi
{
    private readonly HttpClient _client;

    public XiaomiApi(string serviceToken, string userId)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Cookie", $"userId={userId}; serviceToken={serviceToken};");
    }

    public async Task<string> GetDeviceList()
    {
        var url = "https://api.io.mi.com/app/home/device_list";
        var body = "{}";
        var res = await _client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        return await res.Content.ReadAsStringAsync();
    }

    public async Task<string> ToggleLight(string did, string model, bool on)
    {
        var url = "https://api.io.mi.com/app/control/dev";
        var body = JsonSerializer.Serialize(new
        {
            did,
            model,
            method = "set_power",
            @params = new object[] { on ? "on" : "off", "smooth", 500 }
        });
        var res = await _client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        return await res.Content.ReadAsStringAsync();
    }

    public async Task<string> SetColor(string did, string model, int r, int g, int b)
    {
        int rgb = (r << 16) + (g << 8) + b;
        var url = "https://api.io.mi.com/app/control/dev";
        var body = JsonSerializer.Serialize(new
        {
            did,
            model,
            method = "set_rgb",
            @params = new object[] { rgb }
        });
        var res = await _client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        return await res.Content.ReadAsStringAsync();
    }
}
