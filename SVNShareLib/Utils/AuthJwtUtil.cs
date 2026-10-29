using Newtonsoft.Json;
using SVNShareLib.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Utils
{
    public class AuthJwtUtil
    {
        public UserInfo GetUserInfoFromToken(string validToken, out List<Claim> claims)
        {
            JwtClientUtil jwtClientUtil = new JwtClientUtil();
            claims = jwtClientUtil.GetClaims(validToken);

            try
            {
                UserInfo userInfo = new UserInfo();
                if (claims.FirstOrDefault(claim => claim.Type == "AppID") != null)
                {
                    int AppID = Convert.ToInt32(claims.First(claim => claim.Type == "AppID").Value);
                    userInfo.AppID = AppID;
                }
                if (claims.FirstOrDefault(claim => claim.Type == "CompanyID") != null)
                {
                    int CompanyID = Convert.ToInt32(claims.First(claim => claim.Type == "CompanyID").Value);
                    userInfo.CompanyID = CompanyID;
                }

                if (claims.FirstOrDefault(claim => claim.Type == "Email") != null)
                {
                    string EmailAddress = claims.First(claim => claim.Type == "Email").Value;
                    userInfo.EmailAddress = EmailAddress;
                }
                if (claims.FirstOrDefault(claim => claim.Type == "FullName") != null)
                {
                    string FullName = claims.First(claim => claim.Type == "FullName").Value;
                    userInfo.FullName = FullName;
                }
                if (claims.FirstOrDefault(claim => claim.Type == "UserName") != null)
                {
                    string UserName = claims.First(claim => claim.Type == "UserName").Value;
                    userInfo.UserName = UserName;
                }
                if (claims.FirstOrDefault(claim => claim.Type == "UserID") != null)
                {
                    string ID = claims.First(claim => claim.Type == "UserID").Value;
                    userInfo.ID = ID;
                }





                var roleClaims = claims.Where(x => x.Type.ToLower() == "roles").ToList();

                foreach (var item in roleClaims)
                {
                    userInfo.Roles.Add(item.Value);
                }
                string strObjectRights = claims.First(claim => claim.Type.ToLower() == "objectrights").Value;

                Dictionary<string, List<string>> ObjectRights = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(strObjectRights);
                userInfo.ObjectRights = ObjectRights;

                return userInfo;
            }
            catch (Exception ex)
            {
                string error = ex.Message;

                return null;
            }

        }
    }
}
