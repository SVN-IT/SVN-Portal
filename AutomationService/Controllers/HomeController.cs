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

        // 1. Mở Selenium để login Xiaomi
        var options = new ChromeOptions();
        options.AddArgument("--disable-blink-features=AutomationControlled");

        string serviceToken = null;
        string userId = null;

        using (var driver = new ChromeDriver(options))
        {
            driver.Navigate().GoToUrl("https://account.xiaomi.com/pass/serviceLogin?sid=xiaomiio");
            Console.WriteLine("👉 Hãy login bằng tài khoản Xiaomi trong cửa sổ Chrome...");

            // Chờ user login thành công
            bool loggedIn = false;
            while (!loggedIn)
            {
                var cookies = driver.Manage().Cookies.AllCookies;

                var tokenCookie = cookies.FirstOrDefault(c => c.Name == "serviceToken");
                var userIdCookie = cookies.FirstOrDefault(c => c.Name == "userId");

                if (tokenCookie != null && userIdCookie != null)
                {
                    serviceToken = tokenCookie.Value;
                    userId = userIdCookie.Value;
                    loggedIn = true;
                }
                else
                {
                    await Task.Delay(2000);
                }
            }

            Console.WriteLine("✅ Login thành công!");
            Console.WriteLine($"serviceToken = {serviceToken}");
            Console.WriteLine($"userId = {userId}");
        }

        // 2. Tạo API client
        var api = new XiaomiApi(serviceToken, userId);

        // 3. Lấy danh sách device
        var devices = await api.GetDeviceList();
        Console.WriteLine("Danh sách thiết bị:");
        Console.WriteLine(devices);

        // TODO: Parse JSON để lấy did & model của đèn
        string did = "YOUR_DEVICE_DID";       // thay bằng did từ devices
        string model = "yeelink.light.color1"; // ví dụ model đèn Yeelight

        // 4. Bật đèn
        var res1 = await api.ToggleLight(did, model, true);
        Console.WriteLine("Bật đèn: " + res1);

        // 5. Đổi màu đèn sang đỏ
        var res2 = await api.SetColor(did, model, 255, 0, 0);
        Console.WriteLine("Đổi màu: " + res2);

        // 6. Tắt đèn
        var res3 = await api.ToggleLight(did, model, false);
        Console.WriteLine("Tắt đèn: " + res3);
        return View();
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
