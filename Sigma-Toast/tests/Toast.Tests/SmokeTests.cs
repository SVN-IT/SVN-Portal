using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Toast.Tests;

/// <summary>Kiểm tra app khởi động được và các trang cơ bản phản hồi (không cần DB).</summary>
public class SmokeTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    [Fact]
    public async Task Login_page_renders()
    {
        var res = await factory.CreateClient().GetAsync("/toast/admin/account/login");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("Đăng nhập", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Root_redirects_to_station()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/toast", res.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Station_is_open_without_login()
    {
        var res = await factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync("/toast");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("Trạm quét", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_requires_login_and_login_lands_on_admin_home()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/toast/admin");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/toast/admin/account/login", res.Headers.Location?.PathAndQuery);

        var loginPage = await client.GetStringAsync("/toast/admin/account/login");
        var login = await client.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        Assert.Equal("/toast/admin", login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Layout_css_links_are_resolved_and_served()
    {
        // ~/ chỉ được đổi thành đường dẫn thật khi _ViewImports (tag helper) áp dụng cho layout trong Shared/
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/toast/admin/account/login");
        Assert.DoesNotContain("href=\"~/", html);
        var css = await client.GetAsync("/themes/vuexy/vendor/css/core.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
    }
}
