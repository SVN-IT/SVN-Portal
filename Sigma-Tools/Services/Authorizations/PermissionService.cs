using Newtonsoft.Json;
using SVNShareLib.Response;

namespace Sigma_Tools.Services.Authorizations
{
    public class PermissionService : IPermissionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool HasRight(string objectName, string right)
        {
            var session = _httpContextAccessor.HttpContext?.Session;

            if (session == null)
                return false;

            var json = session.GetString("ObjectRights");

            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                var objectRights =
                    JsonConvert.DeserializeObject<
                        Dictionary<string, List<string>>>(json);

                return objectRights != null &&
                       objectRights.TryGetValue(objectName, out var rights) &&
                       rights.Contains(
                           right,
                           StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public UserInfo GetCurrentUser()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return null;
            var json = session.GetString("UserInfo");
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<UserInfo>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
