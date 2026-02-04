namespace SVN_Portal.Services.Authentication
{
    public class AuthenticationAPIConfig
    {
        public string ProductMode { get; set; }
        public string ProdBaseAPIUrl { get; set; }
        public string DevBaseAPIUrl { get; set; }
        public string GetAppRolesURL { get; set; }
        public string GetAppRoleURL { get; set; }
        public string CreateAppRoleURL { get; set; }
        public string UpdateAppRoleURL { get; set; }
        public string DeleteAppRoleURL { get; set; }
        public string GetAppObjectsURL { get; set; }
        public string GetAppObjectByIDURL { get; set; }
        public string GetAppObjectByNumberURL { get; set; }
        public string CreateAppObjectURL { get; set; }
        public string UpdateAppObjectURL { get; set; }
        public string DeleteAppObjectURL { get; set; }
        public string MarkDeletaAppObjectURL { get; set; }
        public string GetCompanyAppURL { get; set; }




        public string GetBaseAPIURL()
        {
            string url = DevBaseAPIUrl;
            if (ProductMode.ToUpper() == "PROD")
            {
                url = ProdBaseAPIUrl;
            }
            return url;
        }

        public string GetAppUsersURL { get; set; }
        public string GetAppUsersByDepartmentURL { get; set; }
        public string GetAppUsersByManagerURL { get; set; }
        public string GetAppUserByIDURL { get; set; }
        public string GetAppUserByNumberURL { get; set; }
        public string CreateAppUserURL { get; set; }
        public string UpdateAppUserURL { get; set; }
        public string MarkDeleteAppUserURL { get; set; }
        public string AdminChangePassURL { get; set; }
        public string LoginURL { get; set; }
        public string ReNewTokenURL { get; set; }
    }
}
