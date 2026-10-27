using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SVN_Portal.Services.Configurations;

namespace SVN_Portal.Areas.TwistAdmin.Controllers
{
    // Base cho mọi màn hình trong khu quản trị Twist (/twist/admin/...).
    // Kế thừa class này là tự có Area + bắt buộc đăng nhập bằng cookie riêng TwistAdminCookie.
    // Thêm màn mới: tạo controller kế thừa class này với [Route("twist/admin/<ten-man>")], rồi thêm 1 dòng menu trong _AdminLayout.
    [Area("TwistAdmin")]
    [Authorize(AuthenticationSchemes = TwistAdminAuthConfig.Scheme)]
    public abstract class TwistAdminControllerBase : Controller
    {
    }
}
