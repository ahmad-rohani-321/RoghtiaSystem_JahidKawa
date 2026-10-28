using System.ComponentModel.DataAnnotations;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class TreatmentRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int? PatientId { get; set; }
        [RegularExpression(@"^$|^[1-9][0-9]{1,2}/[1-9][0-9]{1,2}$"), StringLength(16)]
        public string? BloodPressure { get; set; }
        [StringLength(2000)]
        public string? AdditionalInstructions { get; set; }
        [Required, MaxLength(20)]
        public List<int>? ClinicalNoteIds { get; set; }
        [Required, MaxLength(20)]
        public List<int>? AdditionalInstructionIds { get; set; }
        [MaxLength(20)]
        public List<int>? DiagnosticIds { get; set; } = [];
        [MaxLength(20)]
        public List<int>? InvestigationIds { get; set; } = [];
        // Older clients sent one combined list; keep accepting it during upgrades.
        [MaxLength(20)]
        public List<int>? DiagnosticInvestigationIds { get; set; }
        [Required, MaxLength(30)]
        public List<PrescriptionItemRequest>? PrescriptionItems { get; set; }
        public bool SaveAsTemplate { get; set; }
        [Range(1, int.MaxValue)]
        public int? TemplateId { get; set; }
        [StringLength(100)]
        public string? TemplateName { get; set; }
        public bool AllowInsufficientStock { get; set; }
    }

    public class PrescriptionItemRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int? MedicationId { get; set; }
        [Required, StringLength(100, MinimumLength = 1)]
        public string? Duration { get; set; }
        [Required, Range(1, int.MaxValue)]
        public int? Dose { get; set; }
        [Required, StringLength(64, MinimumLength = 1)]
        public string? Frequency { get; set; }
    }

    public class ClinicalNoteRequest
    {
        [Required, StringLength(300, MinimumLength = 1)]
        public string? Text { get; set; }
    }

    public class AdditionalInstructionRequest
    {
        [Required, StringLength(300, MinimumLength = 1)]
        public string? Text { get; set; }
    }

    public class DiagnosticInvestigationRequest
    {
        [Required, RegularExpression("^(Diagnostic|Investigation)$")]
        public string? Kind { get; set; }
        [Required, StringLength(300, MinimumLength = 1)]
        public string? Text { get; set; }
    }

    public class TreatmentQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
        [Range(1, int.MaxValue)]
        public int? PatientId { get; set; }
        [StringLength(200)]
        public string? PatientName { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }

    public record ClinicalNoteResponse(int Id, string Text);
    public record AdditionalInstructionResponse(int Id, string Text);
    public record DiagnosticInvestigationResponse(int Id, string Text, string Kind);
    public record PrescriptionItemResponse(int Id, int Position, int MedicationId, string MedicationName, string MedicationType, int Dose, string Frequency, string Duration);
    public record TreatmentPatientResponse(int Id, string Code, string Name, string Phone, int Age, string Gender, string Address);
    public record TreatmentSummaryResponse(int Id, DateTime RecordedAtUtc, TreatmentPatientResponse Patient, string BloodPressure, int MedicationCount, int ClinicalNoteCount);
    public record TreatmentResponse(int Id, DateTime RecordedAtUtc, TreatmentPatientResponse Patient, string BloodPressure, string AdditionalInstructions,
        IReadOnlyList<ClinicalNoteResponse> ClinicalNotes, IReadOnlyList<AdditionalInstructionResponse> AdditionalInstructionItems,
        IReadOnlyList<DiagnosticInvestigationResponse> DiagnosticInvestigations, IReadOnlyList<DiagnosticInvestigationResponse> Diagnostics,
        IReadOnlyList<DiagnosticInvestigationResponse> Investigations, IReadOnlyList<PrescriptionItemResponse> PrescriptionItems);
    public record TreatmentListResponse(IReadOnlyList<TreatmentSummaryResponse> Items, int Total, int Page, int PageSize);
    public class TreatmentTemplateData
    {
        public string BloodPressure { get; set; } = "120/80";
        public string AdditionalInstructions { get; set; } = string.Empty;
        public List<int> ClinicalNoteIds { get; set; } = [];
        public List<int> AdditionalInstructionIds { get; set; } = [];
        public List<int> DiagnosticIds { get; set; } = [];
        public List<int> InvestigationIds { get; set; } = [];
        // Older saved template JSON used a single combined list.
        public List<int> DiagnosticInvestigationIds { get; set; } = [];
        public List<TreatmentTemplateItem> PrescriptionItems { get; set; } = [];
    }
    public record TreatmentTemplateItem(int MedicationId, int Dose, string Frequency, string Duration);
    public record TreatmentTemplateResponse(int Id, string Name, string BloodPressure, string AdditionalInstructions,
        IReadOnlyList<int> ClinicalNoteIds, IReadOnlyList<int> AdditionalInstructionIds, IReadOnlyList<int> DiagnosticIds,
        IReadOnlyList<int> InvestigationIds, IReadOnlyList<int> DiagnosticInvestigationIds, IReadOnlyList<TreatmentTemplateItem> PrescriptionItems);
}
