using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public class AuthOptions
    {
        private const string DevelopmentApplicationName = "Roghtia.Development";
        private const string DevelopmentSecretsPurpose = "Roghtia.Development.AuthSecrets.v1";
        private const string LegacySecretsFileName = "auth-secrets.dat";
        private const string MachineSecretsFileName = "auth-secrets.machine.dat";
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
                var secrets = LoadDevelopmentSecrets(environment.ContentRootPath, string.IsNullOrWhiteSpace(connectionString));
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

        // Run once as the Windows account that created the legacy .local/keys ring.
        // The payload is unchanged; only its Data Protection wrapper moves to machine scope.
        public static void MigrateDevelopmentSecrets(IWebHostEnvironment environment)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Development-secret migration is only available in the Development environment.");

            var directory = new DirectoryInfo(Path.Combine(environment.ContentRootPath, ".local"));
            var legacyPath = Path.Combine(directory.FullName, LegacySecretsFileName);
            if (!File.Exists(legacyPath))
                throw new InvalidOperationException("No legacy development secret was found at .local/auth-secrets.dat. Nothing was changed.");

            using var initializationLock = AcquireInitializationLock(Path.Combine(directory.FullName, "auth-secrets.lock"));
            var machinePath = Path.Combine(directory.FullName, MachineSecretsFileName);
            if (File.Exists(machinePath))
            {
                _ = ReadSecrets(CreateMachineProvider(directory), machinePath);
                return;
            }

            Dictionary<string, string> secrets;
            try { secrets = ReadSecrets(CreateLegacyProvider(directory), legacyPath); }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("The legacy development secret can only be migrated by the Windows account and profile that created .local/auth-secrets.dat and .local/keys. No files or database records were changed.", exception);
            }
            WriteSecretsAtomically(machinePath, CreateMachineProvider(directory).CreateProtector(DevelopmentSecretsPurpose).Protect(JsonSerializer.Serialize(secrets)));
        }

        private static Dictionary<string, string> LoadDevelopmentSecrets(string contentRoot, bool usesDefaultDatabase)
        {
            var directory = new DirectoryInfo(Path.Combine(contentRoot, ".local"));
            directory.Create();
            // Serialize the existence check, key-ring loading and secret creation across server processes.
            using var initializationLock = AcquireInitializationLock(Path.Combine(directory.FullName, "auth-secrets.lock"));
            var path = Path.Combine(directory.FullName, MachineSecretsFileName);
            var legacyPath = Path.Combine(directory.FullName, LegacySecretsFileName);
            if (!File.Exists(path) && File.Exists(legacyPath))
                throw new InvalidOperationException("Legacy development secrets were found. Run `dotnet run -- --migrate-development-secrets` once using the Windows account that created .local/keys, then start the server again. No files or database records were changed.");
            if (!File.Exists(path) && usesDefaultDatabase && File.Exists(Path.Combine(contentRoot, "RoghtiaSystemDatabase.db")))
                throw new InvalidOperationException("Development secrets are missing, but RoghtiaSystemDatabase.db already exists. Restore the matching .local directory or configure Auth:SigningKey and ConnectionStrings:MainDatabase with the original database password. No replacement secrets were generated; the database was not changed.");

            var provider = CreateMachineProvider(directory);
            var protector = provider.CreateProtector(DevelopmentSecretsPurpose);
            try
            {
                if (File.Exists(path))
                {
                    return ReadSecrets(provider, path);
                }

                var generated = new Dictionary<string, string>
                {
                    ["SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                    ["DatabaseKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                };
                WriteSecretsAtomically(path, protector.Protect(JsonSerializer.Serialize(generated)));
                return generated;
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("Development secrets could not be decrypted or protected. On Windows, run the server using the same Windows account and user profile that created .local/auth-secrets.dat and .local/keys. If those files were moved, restore their matching backup, or configure Auth:SigningKey and ConnectionStrings:MainDatabase with the original database password. Do not delete .local or the database; no replacement secrets were generated.", exception);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Saved development secrets are unreadable. Restore the matching .local directory or configure Auth:SigningKey and ConnectionStrings:MainDatabase with the original database password. Existing secrets and the database were not replaced.", exception);
            }
        }

        private static IDataProtectionProvider CreateLegacyProvider(DirectoryInfo directory)
            => DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory.FullName, "keys")), settings =>
            {
                settings.SetApplicationName(DevelopmentApplicationName);
                if (OperatingSystem.IsWindows()) settings.ProtectKeysWithDpapi();
            });

        private static IDataProtectionProvider CreateMachineProvider(DirectoryInfo directory)
            => DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory.FullName, "machine-keys")), settings =>
            {
                settings.SetApplicationName(DevelopmentApplicationName);
                if (OperatingSystem.IsWindows()) settings.ProtectKeysWithDpapi(protectToLocalMachine: true);
            });

        private static Dictionary<string, string> ReadSecrets(IDataProtectionProvider provider, string path)
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(provider.CreateProtector(DevelopmentSecretsPurpose).Unprotect(File.ReadAllText(path)));
            if (!IsValidDevelopmentSecret(secrets, "SigningKey", 64) || !IsValidDevelopmentSecret(secrets, "DatabaseKey", 32))
                throw new InvalidOperationException("Saved development secrets are invalid. Restore the matching .local directory or configure Auth:SigningKey and ConnectionStrings:MainDatabase with the original database password. Existing secrets and the database were not replaced.");
            return secrets!;
        }

        private static bool IsValidDevelopmentSecret(Dictionary<string, string>? secrets, string name, int byteCount)
        {
            if (secrets is null || !secrets.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)) return false;
            Span<byte> decoded = stackalloc byte[byteCount];
            try { return Convert.TryFromBase64String(value, decoded, out var written) && written == byteCount; }
            finally { CryptographicOperations.ZeroMemory(decoded); }
        }

        private static FileStream AcquireInitializationLock(string path)
        {
            var started = Stopwatch.GetTimestamp();
            while (true)
            {
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException exception) when ((exception.HResult & 0xffff) is 11 or 32 or 33)
                {
                    if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(10))
                        throw new InvalidOperationException("Another server process is initializing development secrets. Stop duplicate server starts and try again. Existing secrets and the database were not replaced.", exception);
                    Thread.Sleep(100);
                }
            }
        }

        private static void WriteSecretsAtomically(string path, string protectedSecrets)
        {
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                    writer.Write(protectedSecrets);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
