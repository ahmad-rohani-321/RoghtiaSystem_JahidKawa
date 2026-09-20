using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using System.Security.Cryptography;
using System.Text;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<Users> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
        private readonly Users _dummyUser = new();
        private readonly string _dummyHash;

        public PasswordService() => _dummyHash = _hasher.HashPassword(_dummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        public void SetPassword(Users user, string password)
        {
            user.PasswordHash = Encoding.UTF8.GetBytes(_hasher.HashPassword(user, password));
            user.PasswordSalt = Array.Empty<byte>();
            user.PasswordVersion = 1;
        }

        public bool Verify(Users? user, string password, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (user?.PasswordHash == null)
            {
                _hasher.VerifyHashedPassword(_dummyUser, _dummyHash, password);
                return false;
            }
            if (user.PasswordVersion == 1)
            {
                try
                {
                    var result = _hasher.VerifyHashedPassword(user, Encoding.UTF8.GetString(user.PasswordHash), password);
                    needsUpgrade = result == PasswordVerificationResult.SuccessRehashNeeded;
                    return result != PasswordVerificationResult.Failed;
                }
                catch (FormatException) { return false; }
            }
            // Verify old ASCII HMAC records and upgrade on a successful login.
            if (user.PasswordVersion != 0 || user.PasswordSalt is not { Length: > 0 } || user.PasswordHash.Length != 64 || password.Any(c => c > 127)) return false;
            using var hmac = new HMACSHA512(user.PasswordSalt);
            var matches = CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(Encoding.ASCII.GetBytes(password)), user.PasswordHash);
            needsUpgrade = matches;
            return matches;
        }
    }
}
