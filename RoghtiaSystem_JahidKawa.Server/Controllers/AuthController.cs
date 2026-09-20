using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.Services;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Security.Claims;

namespace RoghtiaSystem_JahidKawa.Server.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AuthController(MainDbContext context, PasswordService passwords, TokenService tokens) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] Login request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName))
                return BadRequest(new AuthError("VALIDATION_ERROR", "کارن نوم سم نه دی."));
            var normalizedName = UserNames.Normalize(request.UserName);
            var user = await context.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedName, cancellationToken);
            if (!passwords.Verify(user, request.Password, out bool needsUpgrade))
                return Unauthorized(new AuthError("INVALID_CREDENTIALS", "کارن نوم یا پټنوم ناسم دی."));
            if (needsUpgrade)
            {
                passwords.SetPassword(user!, request.Password);
                await context.SaveChangesAsync(cancellationToken);
            }
            return Ok(tokens.Create(user!, request.RememberMe));
        }

        [AllowAnonymous]
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] Login request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName) || request.Password.Length < 8)
                return BadRequest(new AuthError("VALIDATION_ERROR", "کارن نوم او پټنوم وګورئ. پټنوم باید لږ تر لږه ۸ توري ولري."));
            var normalizedName = UserNames.Normalize(request.UserName);
            if (await context.Users.AnyAsync(u => u.NormalizedUserName == normalizedName, cancellationToken))
                return Conflict(new AuthError("USERNAME_TAKEN", "دا کارن نوم مخکې کارول شوی دی."));
            var user = new Users { UserName = UserNames.Clean(request.UserName), NormalizedUserName = normalizedName };
            passwords.SetPassword(user, request.Password);
            context.Users.Add(user);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // The unique index also protects concurrent registration requests.
                return Conflict(new AuthError("USERNAME_TAKEN", "دا کارن نوم مخکې کارول شوی دی."));
            }
            return Ok(tokens.Create(user, false));
        }

        [Authorize]
        [HttpPost("change-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePassword request, CancellationToken cancellationToken)
        {
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new AuthError("VALIDATION_ERROR", "نوی پټنوم باید له اوسني پټنوم څخه توپیر ولري."));
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
                return Unauthorized(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."));
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
                return Unauthorized(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."));
            if (!passwords.Verify(user, request.CurrentPassword, out _))
                return BadRequest(new AuthError("PASSWORD_INCORRECT", "اوسنی پټنوم ناسم دی."));

            passwords.SetPassword(user, request.NewPassword);
            user.TokenVersion++;
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Unauthorized(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."));
            }
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
                return Unauthorized(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."));
            var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            return user == null
                ? Unauthorized(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."))
                : Ok(TokenService.GetUser(user));
        }
    }
}

