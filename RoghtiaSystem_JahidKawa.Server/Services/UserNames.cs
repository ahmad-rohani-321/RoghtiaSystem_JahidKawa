using System.Text;

namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public static class UserNames
    {
        public static string Clean(string name) => name.Trim().Normalize(NormalizationForm.FormC);
        public static string Normalize(string name) => Clean(name).ToUpperInvariant();
        public static bool IsValid(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 64 && !name.Any(char.IsControl);
    }
}
