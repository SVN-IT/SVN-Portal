using Newtonsoft.Json;
using SVN_Authentication_Portal.Configurations;
using SVN_Authentication_Portal.Models;
using SVN_Authentication_Portal.Utilities;
using SVNShareLib;
using SVNShareLib.Utils;

namespace SVN_Authentication_Portal.Services
{
    public class AccountService
    {
        IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _factory;
        IConfiguration _configuration;
        AuthenticationAPIConfig _serviceConfig;
        AppConfig _appConfig;
        SVNSignInManager _msasignInManager;
        public AccountService(IHttpClientFactory factory,
            IHttpContextAccessor httpContextAccessor,
            AuthenticationAPIConfig serviceConfig,
            AppConfig appConfig,
            IConfiguration configuration, SVNSignInManager msasignInManager
            )
        {
            _factory = factory;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _serviceConfig = serviceConfig;
            _appConfig = appConfig;
            _msasignInManager = msasignInManager;

        }
        /// <summary>
        ///  httpClient.DefaultRequestHeaders.Authorization =
        ///  new AuthenticationHeaderValue("Bearer", "Your Oauth token");
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<bool> Login(LoginViewModel viewModel)
        {
            bool isLogin = false;
            LoginModel model = new LoginModel()
            {
                AppID = _appConfig.AppID,
                CompanyID = _appConfig.CompanyID,
                KeepLogined = viewModel.KeepLogined,
                Password = viewModel.Pwd,
                UserName = viewModel.UserName,
                UserType = "UserID",

            };
            try
            {
                HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(_serviceConfig.GetBaseAPIURL(), 1000);
                string strAcessURL = _serviceConfig.LoginURL;
                LoginInfo loginInfo = null;
                //===>call api===>


                string json = JsonConvert.SerializeObject(model);
                var response = await httpClientHelper.PostRequest(strAcessURL, model, new CancellationToken(false));
                if (response != null)
                {
                    isLogin = response.Content != null && response.OK;
                    //==> IsLogin=true Client Login process====>

                    loginInfo = JsonConvert.DeserializeObject<LoginInfo>(response.Content.ToString()); //(LoginInfo)processResult.Content;

                    //===>save cookier JwtData
                    SVNUserInfo userInfo = new SVNUserInfo();
                    userInfo.LoginInfo = loginInfo;
                    await _msasignInManager.SignInAsync(userInfo);


                    return isLogin;
                    //===>
                }
                else
                {
                    return isLogin;
                }
                //===end call api=========>

                if (!isLogin)
                {
                    return isLogin;
                }

                ////==> IsLogin=true Client Login process====>

                //loginInfo = JsonConvert.DeserializeObject<LoginInfo>(response.Content.ToString()); //(LoginInfo)processResult.Content;

                ////===>save cookier JwtData
                //SVNUserInfo userInfo = new SVNUserInfo();
                //userInfo.LoginInfo = loginInfo;
                //await _msasignInManager.SignInAsync(userInfo);


                //return isLogin;
                ////===>

            }
            catch (Exception ex)
            {
                string err = ex.Message;
            }
            //===> end login process
            return isLogin;
        }

        public async Task<SVNUserInfo> LoginByEmail(LoginViewModel viewModel)
        {
            bool isLogin = false;
            LoginModel model = new LoginModel()
            {
                AppID = _appConfig.AppID,
                CompanyID = _appConfig.CompanyID,
                KeepLogined = viewModel.KeepLogined,
                Password = viewModel.Pwd,
                UserName = viewModel.UserName,
                UserType = "UserID",

            };
            try
            {
                HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(_serviceConfig.GetBaseAPIURL(), 1000);
                string strAcessURL = _serviceConfig.LoginByEmailURL;
                LoginInfo loginInfo = null;
                //===>call api===>


                string json = JsonConvert.SerializeObject(model);
                var response = await httpClientHelper.PostRequest(strAcessURL, model, new CancellationToken(false));
                if (response != null)
                {
                    isLogin = response.Content != null && response.OK;
                    //==> IsLogin=true Client Login process====>

                    loginInfo = JsonConvert.DeserializeObject<LoginInfo>(response.Content.ToString()); //(LoginInfo)processResult.Content;

                    //===>save cookier JwtData
                    SVNUserInfo userInfo = new SVNUserInfo();
                    userInfo.LoginInfo = loginInfo;
                    await _msasignInManager.SignInAsync(userInfo);

                    return userInfo;
                    //===>
                }
                else
                {
                    return null;
                }
                //===end call api=========>

                if (!isLogin)
                {
                    return null;
                }

                ////==> IsLogin=true Client Login process====>

                //loginInfo = JsonConvert.DeserializeObject<LoginInfo>(response.Content.ToString()); //(LoginInfo)processResult.Content;

                ////===>save cookier JwtData
                //SVNUserInfo userInfo = new SVNUserInfo();
                //userInfo.LoginInfo = loginInfo;
                //await _msasignInManager.SignInAsync(userInfo);


                //return isLogin;
                ////===>

            }
            catch (Exception ex)
            {
                string err = ex.Message;
            }
            //===> end login process
            return null;
        }

        /// <summary>
        /// renew token base on exist token
        /// neu renew thanh cong thi tra ve new token
        /// neu renew that bai tra ve null
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<JwtData?> ReNewToken(JwtData model)
        {
            //Cap nhat cookie information

            JwtClientUtil jwtClientUtil = new JwtClientUtil();
            if (jwtClientUtil.IsAccessTokenExpired(model.AccessToken))
            {

                return model;
            }

            JwtData reNewToken = null;
            try
            {
                HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(_serviceConfig.GetBaseAPIURL(), 1000);
                string strAcessURL = _serviceConfig.ReNewTokenURL;
                //===>call api===>
                var response = await httpClientHelper.PostRequest(strAcessURL, model, new CancellationToken(false));
                if (response != null)
                {
                    if (response.OK)
                    {
                        reNewToken = JsonConvert.DeserializeObject<JwtData>(response.Content.ToString()); //(LoginInfo)processResult.Content;
                    }

                    if (reNewToken != null)
                        _msasignInManager.SaveJwtData(reNewToken);
                    return reNewToken;
                }
                else
                {
                    return null;
                }
                //===end call api=========>




            }
            catch (Exception ex)
            {
                reNewToken = null;
                string err = ex.Message;
            }
            //===> end login process
            return reNewToken;
        }
    }
}
