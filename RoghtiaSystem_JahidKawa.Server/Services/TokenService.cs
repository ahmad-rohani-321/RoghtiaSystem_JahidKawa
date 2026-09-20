using Microsoft.IdentityModel.Tokens;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public class TokenService(AuthOptions options)
    {
        public const string VersionClaim = "token_version";
        public static AuthUser GetUser(Users user) => new(user.Id, user.UserName!, [user.Role], []);

        public AuthResponse Create(Users user, bool rememberMe)
        {
            var now = DateTime.UtcNow;
            var expires = rememberMe ? now.AddDays(options.RememberDays) : now.AddHours(options.SessionHours);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(VersionClaim, user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(ClaimTypes.Role, user.Role)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
            var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, now, expires, new SigningCredentials(key, SecurityAlgorithms.HmacSha512));
            return new(new JwtSecurityTokenHandler().WriteToken(token), expires, GetUser(user));
        }
    }
}
