namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public record ReportsResponse(
        DateTime FromUtc,
        DateTime ToUtc,
        TreatmentReportResponse Treatments,
        IReadOnlyList<MedicationReportItem> Medications,
        IReadOnlyList<PatientReportItem> Patients,
        IReadOnlyList<TextUsageReportItem> ClinicalNotes,
        IReadOnlyList<TextUsageReportItem> AdditionalInstructions);

    public record TreatmentReportResponse(
        int Total,
        int WithPrescription,
        IReadOnlyList<DailyTreatmentReportItem> ByDay,
        IReadOnlyList<PatientVisitReportItem> MostVisitedPatients);

    public record DailyTreatmentReportItem(string Date, int Treatments, int Prescriptions);
    public record PatientVisitReportItem(int PatientId, string PatientCode, string PatientName, int Visits);
    public record PatientReportItem(int Id, string Code, string Name, string Phone, int Age, string Gender, string Address, int Visits);
    public record MedicationReportItem(int Id, string Name, string Type, int CurrentQuantity, int PrescriptionLines, long PrescribedDose, long DeductedDose);
    public record TextUsageReportItem(string Text, int Uses);
}
