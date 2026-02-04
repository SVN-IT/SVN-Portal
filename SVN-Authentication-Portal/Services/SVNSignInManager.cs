using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using SVN_Authentication_Portal.Models;
using SVN_Authentication_Portal.Utilities;
using System.Security.Claims;

namespace SVN_Authentication_Portal.Services
{
    public class SVNSignInManager
    {

        IHttpContextAccessor _contextAccessor;
        static SVNUserInfo _userInfo;
        public SVNSignInManager(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;

        }

        public bool IsSignedIn()
        {
            //return base.IsSignedIn(principal);

            if (_contextAccessor != null)
            {
                JwtData jwtData = _contextAccessor.HttpContext.Session.GetObject<JwtData>("JwtData");
                if (jwtData == null) return false;
                return _contextAccessor.HttpContext.User.Identity.IsAuthenticated;
            }
            else
            {
                return false;
            }


        }
        /// <summary>
        /// kiem tra role co trong logined user
        /// 
        /// </summary>
        /// <param name="strRoles">role set tren view:
        /// HasPermission("about;read,list")</param>
        /// <returns></returns>
        public bool HasPermission(string strRoles)
        {
            List<string> roles = strRoles.Split(";").ToList();
            string objectName = roles[0]; //company,about
            List<string> requiredRoles = roles[1].Split(",").ToList();

            //gia su sau khi check ra perm cua user la 
            List<string> userRights = GetUserObjectRights(objectName);
            bool authorized = false;
            foreach (string userRight in userRights)
            {
                if (requiredRoles.Contains(userRight))
                {
                    authorized = true;
                    break;
                }
            }
            return authorized;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="strroles">la 1 chuoi cac role tren view; ex: admin; user;..</param>
        /// ex: "admin;system"
        /// gia su user login vao co role la admin
        /// <returns></returns>
        public bool IsInRoles(string strroles)
        {
            bool result = false;
            //role cua login 
            List<string> loginRoles = new List<string>()
            {
                "admin"
            };

            //Nếu như người dùng đã đăng nhập, lấy danh sách role của họ
            string currentUserRole = GetUserRole();
            if (!string.IsNullOrWhiteSpace(currentUserRole))
            {
                loginRoles = currentUserRole.Split(",").ToList();
            }


            //admin;system
            List<string> requiredRoles = strroles.Split(";").ToList();
            foreach (var userRole in loginRoles)
            {
                if (requiredRoles.Contains(userRole))
                {
                    result = true;
                    break;
                }

            }
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectName"></param>
        /// <returns></returns>
        private List<string> GetUserObjectRights(string objectName)
        {
            if (!string.IsNullOrWhiteSpace(objectName))
            {
                objectName = objectName.ToLower();
            }
            List<string> objectPermissions = new List<string>();
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "objectrights");

            if (claims == null)
            {
                return objectPermissions;
            }
            string claimsValue = claims.Value;
            Dictionary<string, List<string>> user_permissions = new Dictionary<string, List<string>>();
            if (!string.IsNullOrEmpty(claimsValue))
            {
                user_permissions = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(claimsValue);
                //userInfo.ObjectRights = user_permissions;
                if (user_permissions != null && user_permissions.ContainsKey(objectName))
                    objectPermissions = user_permissions[objectName];

            }

            return objectPermissions;




            //return new List<string>() { "admin", "user" };
        }

        public string GetUserName()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "username");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetUserEmail()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "email");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetManagerEmail()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "manageremail");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetFullName()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "fullname");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetDepartment()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "department");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetUserRole()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "roles");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public string GetUserID()
        {
            if (!IsSignedIn())
            {
                return string.Empty;
            }
            var claims = _contextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type.ToLower() == "userid");
            if (claims == null)
            {
                return string.Empty;
            }
            return claims.Value;
        }

        public async Task SignInAsync(SVNUserInfo userInfo)
        {
            if (_userInfo == null)
                _userInfo = new SVNUserInfo();

            _userInfo = userInfo;



            LoginInfo loginInfo = userInfo.LoginInfo;



            //===>Jwt client process
            JwtClientUtil jwtUtil = new JwtClientUtil();
            List<Claim> claims = jwtUtil.GetClaims(loginInfo.JwtData.AccessToken);
            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties authenticationProperties = new AuthenticationProperties()
            {
                AllowRefresh = true,
                IsPersistent = userInfo.KeepLogined
            };

            //====login into HttpContext
            ClaimsPrincipal user = new ClaimsPrincipal(identity);

            await _contextAccessor.HttpContext.SignInAsync(user, authenticationProperties);


            _contextAccessor.HttpContext.User = user;

            //===>save cookier JwtData
            SetLogin(_userInfo.LoginInfo);

        }



        /// <summary>
        /// luu them thong tin vao session ngoai thong tin dc luu boi framework
        /// _httpContextAccessor.HttpContext
        /// </summary>
        /// <param name="loginInfo"></param>
        /// <returns></returns>
        private bool SetLogin(LoginInfo loginInfo)
        {
            bool OK = false;

            try
            {
                OK = loginInfo != null;
                _contextAccessor.HttpContext.Session.SetObject("LoginDate", loginInfo.LoginDate);
                _contextAccessor.HttpContext.Session.SetObject("JwtData", loginInfo.JwtData);
            }
            catch (Exception ex)
            {

            }
            finally
            {
            }
            return OK;
        }
        /// <summary>
        /// remove cac thong tin da luu
        /// </summary>
        /// <returns></returns>
        private async Task<bool> SetLogout()
        {
            try
            {
                _userInfo = null;
                await _contextAccessor.HttpContext.SignOutAsync();
                _contextAccessor.HttpContext.Session.Remove("LoginDate");
                _contextAccessor.HttpContext.Session.Remove("JwtData");
                return true;
            }
            catch
            {
                return false;
            }

        }
        public JwtData GetJwtData()
        {
            string loginDate = _contextAccessor.HttpContext.Session.GetString("LoginDate");
            JwtData jwtData = null;
            jwtData = _contextAccessor.HttpContext.Session.GetObject<JwtData>("JwtData");
            if (!string.IsNullOrWhiteSpace(loginDate))
            {
                jwtData = _contextAccessor.HttpContext.Session.GetObject<JwtData>("JwtData");
                return jwtData;
            }
            return null;
        }

        /// <summary>
        /// Save JwtData when renew token only
        /// </summary>
        /// <param name="data"></param>
        public bool SaveJwtData(JwtData data)
        {
            string loginDate = _contextAccessor.HttpContext.Session.GetString("LoginDate");

            if (!string.IsNullOrWhiteSpace(loginDate))
            {
                _contextAccessor.HttpContext.Session.SetObject("JwtData", data);
                return true;
            }
            return false;
        }

        public async Task SignOutAsync()
        {
            await SetLogout();

        }
    }

    public static class HttpRequestExtensions
    {
        public static string? BaseUrl(this HttpRequest req)
        {
            if (req == null) return null;
            var uriBuilder = new UriBuilder(req.Scheme, req.Host.Host, req.Host.Port ?? -1);
            if (uriBuilder.Uri.IsDefaultPort)
            {
                uriBuilder.Port = -1;
            }

            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
