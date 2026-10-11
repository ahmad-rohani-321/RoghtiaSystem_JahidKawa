namespace RoghtiaSystem_JahidKawa.Server.Data.Models
{
    public class Treatment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public Users User { get; set; } = null!;
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public DateTime RecordedAtUtc { get; set; }
        public string BloodPressure { get; set; } = string.Empty;
        public List<PrescriptionItem> PrescriptionItems { get; set; } = [];
        public List<TreatmentClinicalNote> ClinicalNotes { get; set; } = [];
    }

    public class PrescriptionItem
    {
        public int Id { get; set; }
        public int TreatmentId { get; set; }
        public Treatment Treatment { get; set; } = null!;
        public int MedicationId { get; set; }
        public Medication Medication { get; set; } = null!;
        public int Position { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string MedicationType { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public int Refills { get; set; }
    }

    public class ClinicalNote
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public Users User { get; set; } = null!;
        public string Text { get; set; } = string.Empty;
    }

    public class TreatmentClinicalNote
    {
        public int TreatmentId { get; set; }
        public Treatment Treatment { get; set; } = null!;
        public int ClinicalNoteId { get; set; }
        public ClinicalNote ClinicalNote { get; set; } = null!;
    }
}
