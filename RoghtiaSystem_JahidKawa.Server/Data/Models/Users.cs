namespace RoghtiaSystem_JahidKawa.Server.Data.Models
{
    public class Users
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string NormalizedUserName { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public byte[]? PasswordHash { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public byte[]? PasswordSalt { get; set; }
        public int PasswordVersion { get; set; }
        public int TokenVersion { get; set; }
        // Stores the hash of the recovery PIN, never its digits.
        [System.Text.Json.Serialization.JsonIgnore]
        public string PINCode { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonIgnore]
        public int PINFailedAttempts { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime? PINLockedUntilUtc { get; set; }
        public string Role { get; set; } = "User";
    }
}
