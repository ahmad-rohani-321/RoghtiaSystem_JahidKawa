using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using System.Security.Cryptography;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public class PINCodeService
    {
        private readonly PasswordHasher<Users> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
        private readonly Users _dummyUser = new();
        private readonly string _dummyHash;

        public PINCodeService() => _dummyHash = _hasher.HashPassword(_dummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        public static bool IsValid(string? pin) => pin is { Length: >= 6 and <= 10 } && pin.All(character => character is >= '0' and <= '9');

        public void Set(Users user, string pin) => user.PINCode = _hasher.HashPassword(user, pin);

        public bool Verify(Users? user, string pin)
        {
            try
            {
                var hash = string.IsNullOrWhiteSpace(user?.PINCode) ? _dummyHash : user.PINCode;
                return _hasher.VerifyHashedPassword(user ?? _dummyUser, hash, pin) != PasswordVerificationResult.Failed && user != null && !string.IsNullOrWhiteSpace(user.PINCode);
            }
            catch (FormatException) { return false; }
        }
    }
}
