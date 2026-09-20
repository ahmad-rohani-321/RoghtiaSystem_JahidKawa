using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class MedicationRequest
    {
        [Required]
        [StringLength(200)]
        public string? Name { get; set; }

        [Required]
        [StringLength(100)]
        public string? Type { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [JsonNumberHandling(JsonNumberHandling.Strict)]
        public int? Quantity { get; set; }

        [StringLength(1000)]
        public string? Remarks { get; set; }
    }

    public class MedicationQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        [StringLength(200)]
        public string? Search { get; set; }
    }

    public record MedicationUserResponse(int Id, string UserName);
    public record MedicationResponse(int Id, string Name, string Type, int Quantity, string Remarks, MedicationUserResponse User);
    public record MedicationListResponse(IReadOnlyList<MedicationResponse> Items, int Total, int Page, int PageSize);
}
