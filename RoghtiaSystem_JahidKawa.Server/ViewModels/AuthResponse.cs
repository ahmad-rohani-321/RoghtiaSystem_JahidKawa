namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public record AuthUser(int Id, string UserName, string[] Roles, string[] Permissions);
    public record AuthResponse(string Token, DateTime ExpiresAt, AuthUser User);
    public record AuthError(string Code, string Message);
}
