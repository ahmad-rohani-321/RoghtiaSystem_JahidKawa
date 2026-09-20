namespace RoghtiaSystem_JahidKawa.Server.Data.Models
{
    public class Users
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string NormalizedUserName { get; set; } = string.Empty;
        public byte[]? PasswordHash { get; set; }
        public byte[]? PasswordSalt { get; set; }
        public int PasswordVersion { get; set; }
        public int TokenVersion { get; set; }
        public string Role { get; set; } = "User";
    }
}
