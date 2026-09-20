using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public class AuthOptions
    {
        public string SigningKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "roghtia-api";
        public string Audience { get; set; } = "roghtia-client";
        public int SessionHours { get; set; } = 24;
        public int RememberDays { get; set; } = 30;
        public int RateLimitPermitLimit { get; set; } = 20;

        public static (AuthOptions Auth, string ConnectionString) Load(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var options = configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
            string? connectionString = configuration.GetConnectionString("MainDatabase");
            if (string.IsNullOrWhiteSpace(options.SigningKey) || string.IsNullOrWhiteSpace(connectionString))
            {
                if (!environment.IsDevelopment())
                    throw new InvalidOperationException("Configure Auth:SigningKey and ConnectionStrings:MainDatabase before starting the server.");
                var directory = new DirectoryInfo(Path.Combine(environment.ContentRootPath, ".local"));
                directory.Create();
                var provider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory.FullName, "keys")), settings =>
                {
                    settings.SetApplicationName("Roghtia.Development");
                    if (OperatingSystem.IsWindows()) settings.ProtectKeysWithDpapi();
                });
                var protector = provider.CreateProtector("Roghtia.Development.AuthSecrets.v1");
                var path = Path.Combine(directory.FullName, "auth-secrets.dat");
                Dictionary<string, string> secrets;
                if (File.Exists(path))
                    secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(protector.Unprotect(File.ReadAllText(path)))!;
                else
                {
                    secrets = new() { ["SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), ["DatabaseKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
                    File.WriteAllText(path, protector.Protect(JsonSerializer.Serialize(secrets)));
                }
                if (string.IsNullOrWhiteSpace(options.SigningKey)) options.SigningKey = secrets["SigningKey"];
                if (string.IsNullOrWhiteSpace(connectionString))
                    connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = Path.Combine(environment.ContentRootPath, "RoghtiaSystemDatabase.db"),
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Password = secrets["DatabaseKey"]
                    }.ConnectionString;
            }
            if (Encoding.UTF8.GetByteCount(options.SigningKey) < 64)
                throw new InvalidOperationException("Auth:SigningKey must contain at least 64 bytes.");
            if (options.SessionHours is < 1 or > 168 || options.RememberDays is < 1 or > 90 || options.RateLimitPermitLimit < 1)
                throw new InvalidOperationException("Authentication lifetime or rate limit settings are invalid.");
            return (options, connectionString);
        }
    }
}
