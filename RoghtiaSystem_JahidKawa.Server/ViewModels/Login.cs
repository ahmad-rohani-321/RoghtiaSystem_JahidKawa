namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class Login
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(64)]
        public string UserName { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128)]
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
