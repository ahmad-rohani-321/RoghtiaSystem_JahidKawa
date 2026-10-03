using System.ComponentModel.DataAnnotations;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class Register
    {
        [Required, StringLength(64)]
        public string UserName { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, RegularExpression("^[0-9]{6,10}$")]
        public string PINCode { get; set; } = string.Empty;
    }
}
