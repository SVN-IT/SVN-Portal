using System.ComponentModel.DataAnnotations;

namespace Sigma_Authentication_Portal_API.Models
{
    public class LoginViewModel
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Pwd { get; set; }
        [Required]
        public bool KeepLogined { get; set; }
        public int AppID { get; set; }
        public int CompanyID { get; set; }
    }
}
