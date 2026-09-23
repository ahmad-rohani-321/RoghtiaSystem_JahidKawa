using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class PatientRequest
    {
        [Required, StringLength(200)]
        public string? Name { get; set; }
        [StringLength(32)]
        public string? Phone { get; set; }
        [Required, Range(0, 150), JsonNumberHandling(JsonNumberHandling.Strict)]
        public int? Age { get; set; }
        [Required, RegularExpression("^(Male|Female|Other)$")]
        public string? Gender { get; set; }
        [StringLength(1000)]
        public string? Address { get; set; }
    }

    public class PatientQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
        [StringLength(200)]
        public string? Search { get; set; }
    }
    public record PatientUserResponse(int Id, string UserName);
    public record PatientResponse(int Id, string Code, string Name, string Phone, int Age, string Gender, string Address, PatientUserResponse User);
    public record PatientListResponse(IReadOnlyList<PatientResponse> Items, int Total, int Page, int PageSize);
}
