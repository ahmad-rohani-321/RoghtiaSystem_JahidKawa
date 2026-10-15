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
    public class AuthController(MainDbContext context, PasswordService passwords, PINCodeService pins, TokenService tokens) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] Login request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Invalid username."));
            var normalizedName = UserNames.Normalize(request.UserName);
            var user = await context.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedName, cancellationToken);
            if (!passwords.Verify(user, request.Password, out bool needsUpgrade))
                return Unauthorized(new AuthError("INVALID_CREDENTIALS", "Incorrect username or password."));
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
        public async Task<IActionResult> Register([FromBody] Register request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName) || !PINCodeService.IsValid(request.PINCode))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Check the username, password and 6 to 10 digit PIN code."));
            var normalizedName = UserNames.Normalize(request.UserName);
            if (await context.Users.AnyAsync(u => u.NormalizedUserName == normalizedName, cancellationToken))
                return Conflict(new AuthError("USERNAME_TAKEN", "This username is already taken."));
            var user = new Users { UserName = UserNames.Clean(request.UserName), NormalizedUserName = normalizedName };
            passwords.SetPassword(user, request.Password);
            pins.Set(user, request.PINCode);
            context.Users.Add(user);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // The unique index also protects concurrent registration requests.
                return Conflict(new AuthError("USERNAME_TAKEN", "This username is already taken."));
            }
            return Ok(tokens.Create(user, false));
        }

        [AllowAnonymous]
        [HttpPost("verify-pin")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyPIN([FromBody] VerifyPINCode request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName) || !PINCodeService.IsValid(request.PINCode))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Username and PIN code."));
            var user = await context.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == UserNames.Normalize(request.UserName), cancellationToken);
            if (!await CheckPINAsync(user, request.PINCode, cancellationToken))
                return Unauthorized(new AuthError("INVALID_PIN", "Username or PIN is incorrect."));
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPassword request, CancellationToken cancellationToken)
        {
            if (!UserNames.IsValid(request.UserName) || !PINCodeService.IsValid(request.PINCode) || request.NewPassword == request.PINCode)
                return BadRequest(new AuthError("VALIDATION_ERROR", "Check username, PIN and new password."));
            var user = await context.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == UserNames.Normalize(request.UserName), cancellationToken);
            if (!await CheckPINAsync(user, request.PINCode, cancellationToken))
                return Unauthorized(new AuthError("INVALID_PIN", "Username or PIN is incorrect."));

            passwords.SetPassword(user!, request.NewPassword);
            user!.TokenVersion++;
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new AuthError("RESET_CONFLICT", "Unable to change password. Please try again."));
            }
            return NoContent();
        }

        [Authorize]
        [HttpPost("set-pin")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> SetPIN([FromBody] SetPINCode request, CancellationToken cancellationToken)
        {
            if (!PINCodeService.IsValid(request.PINCode))
                return BadRequest(new AuthError("VALIDATION_ERROR", "PIN must contain 6 to 10 digits."));
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            if (!passwords.Verify(user, request.CurrentPassword, out _))
                return BadRequest(new AuthError("PASSWORD_INCORRECT", "Current password is incorrect."));

            pins.Set(user, request.PINCode);
            user.PINFailedAttempts = 0;
            user.PINLockedUntilUtc = null;
            await context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private async Task<bool> CheckPINAsync(Users? user, string pin, CancellationToken cancellationToken)
        {
            if (user?.PINLockedUntilUtc > DateTime.UtcNow)
            {
                _ = pins.Verify(null, pin);
                return false;
            }
            if (!pins.Verify(user, pin))
            {
                if (user != null)
                {
                    user.PINFailedAttempts++;
                    if (user.PINFailedAttempts >= 5)
                    {
                        user.PINFailedAttempts = 0;
                        user.PINLockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                    }
                    await context.SaveChangesAsync(cancellationToken);
                }
                return false;
            }
            if (user!.PINFailedAttempts > 0 || user.PINLockedUntilUtc != null)
            {
                user.PINFailedAttempts = 0;
                user.PINLockedUntilUtc = null;
                await context.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        [Authorize]
        [HttpPost("change-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePassword request, CancellationToken cancellationToken)
        {
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new AuthError("VALIDATION_ERROR", "New password must differ from the current password."));
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            if (!passwords.Verify(user, request.CurrentPassword, out _))
                return BadRequest(new AuthError("PASSWORD_INCORRECT", "Current password is incorrect."));

            passwords.SetPassword(user, request.NewPassword);
            user.TokenVersion++;
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            }
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id))
                return Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."));
            var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            return user == null
                ? Unauthorized(new AuthError("UNAUTHENTICATED", "Please sign in again."))
                : Ok(TokenService.GetUser(user));
        }
    }
}

