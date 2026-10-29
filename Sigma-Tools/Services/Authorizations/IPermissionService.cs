using SVNShareLib.Response;

namespace Sigma_Tools.Services.Authorizations
{
    public interface IPermissionService
    {
        bool HasRight(string objectName, string right);

        UserInfo GetCurrentUser();
    }
}
