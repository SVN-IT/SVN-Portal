using System.ComponentModel.DataAnnotations;

namespace SVN_Authentication_Portal.Models
{
    public class LoginViewModel
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Pwd { get; set; }
        [Required]
        public bool KeepLogined { get; set; }
    }
}
