using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SVN_Authentication_Portal.Utilities
{
    //public class JwtClientUtil
    //{
    //    public List<Claim> GetClaims(string accessToken)
    //    {
    //        JwtSecurityTokenHandler jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
    //        SecurityToken securityToken = jwtSecurityTokenHandler.ReadToken(accessToken);
    //        JwtSecurityToken jwtSecurityToken = securityToken as JwtSecurityToken;
    //        return jwtSecurityToken.Claims.ToList();
    //    }

    //    public bool IsAccessTokenExpired(string AccessToken)
    //    {
    //        try
    //        {
    //            JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(AccessToken);
    //            return jwtSecurityToken.ValidTo > DateTime.UtcNow;
    //        }
    //        catch (Exception)
    //        {
    //            return false;
    //        }
    //    }

    //    public bool IsTokenExpiredV1(string accessToken)
    //    {
    //        List<Claim> claims = GetClaims(accessToken);
    //        long utcExpired = long.Parse(claims.FirstOrDefault((Claim x) => x.Type == "exp").Value);
    //        DateTime dateTime = ConvertUnixTimeToDate(utcExpired);
    //        return dateTime > DateTime.UtcNow;
    //    }

    //    public DateTime ConvertUnixTimeToDate(long utcExpired)
    //    {
    //        DateTime result = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    //        result.AddSeconds(utcExpired).ToUniversalTime();
    //        return result;
    //    }
    //}
}
