using System.ComponentModel.DataAnnotations;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class TreatmentRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int? PatientId { get; set; }
        [RegularExpression(@"^$|^[1-9][0-9]{1,2}/[1-9][0-9]{1,2}$"), StringLength(16)]
        public string? BloodPressure { get; set; }
        [Required, MaxLength(20)]
        public List<int>? ClinicalNoteIds { get; set; }
        [Required, MaxLength(30)]
        public List<PrescriptionItemRequest>? PrescriptionItems { get; set; }
    }

    public class PrescriptionItemRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int? MedicationId { get; set; }
        [Required, StringLength(100, MinimumLength = 1)]
        public string? Duration { get; set; }
        [Required, Range(0, 100)]
        public int? Refills { get; set; }
    }

    public class ClinicalNoteRequest
    {
        [Required, StringLength(300, MinimumLength = 1)]
        public string? Text { get; set; }
    }

    public class TreatmentQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
        [StringLength(200)]
        public string? PatientName { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public bool HasPrescription { get; set; }
    }

    public record ClinicalNoteResponse(int Id, string Text);
    public record PrescriptionItemResponse(int Id, int Position, int MedicationId, string MedicationName, string MedicationType, string Duration, int Refills);
    public record TreatmentPatientResponse(int Id, string Code, string Name, string Phone, int Age, string Gender, string Address);
    public record TreatmentSummaryResponse(int Id, DateTime RecordedAtUtc, TreatmentPatientResponse Patient, string BloodPressure, int MedicationCount, int ClinicalNoteCount);
    public record TreatmentResponse(int Id, DateTime RecordedAtUtc, TreatmentPatientResponse Patient, string BloodPressure,
        IReadOnlyList<ClinicalNoteResponse> ClinicalNotes, IReadOnlyList<PrescriptionItemResponse> PrescriptionItems);
    public record TreatmentListResponse(IReadOnlyList<TreatmentSummaryResponse> Items, int Total, int Page, int PageSize);
}
