using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Services;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
SQLitePCL.Batteries_V2.Init();
var (authOptions, connectionString) = AuthOptions.Load(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new AuthError("VALIDATION_ERROR", "مهرباني وکړئ د فورمې معلومات وګورئ."));
});
builder.Services.AddOpenApi();
builder.Services.AddDbContext<MainDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
        ValidateIssuer = true,
        ValidIssuer = authOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = authOptions.Audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
        ClockSkew = TimeSpan.Zero,
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                || !int.TryParse(principal?.FindFirstValue(TokenService.VersionClaim) ?? "0", out int tokenVersion))
            {
                context.Fail("The authentication token is invalid.");
                return;
            }
            var database = context.HttpContext.RequestServices.GetRequiredService<MainDbContext>();
            var currentVersion = await database.Users.AsNoTracking().Where(u => u.Id == id)
                .Select(u => (int?)u.TokenVersion).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentVersion == null || currentVersion != tokenVersion)
                context.Fail("The authentication session has ended.");
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new AuthError("UNAUTHENTICATED", "بیا خپل حساب ته ننوځئ."));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new AuthError("FORBIDDEN", "تاسو د دې کار اجازه نه لرئ."));
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    // New API endpoints require authentication unless explicitly marked anonymous.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authOptions.RateLimitPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new AuthError("TOO_MANY_REQUESTS", "لږ وروسته بیا هڅه وکړئ."), cancellationToken);
    };
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<MainDbContext>());
}
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
else app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();

