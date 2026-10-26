export interface DailyTreatmentReportItem { date: string, treatments: number, prescriptions: number }
export interface PatientVisitReportItem { patientId: number, patientCode: string, patientName: string, visits: number }
export interface PatientReportItem { id: number, code: string, name: string, phone: string, age: number, gender: string, address: string, visits: number }
export interface MedicationReportItem { id: number, name: string, type: string, currentQuantity: number, prescriptionLines: number, prescribedDose: number, deductedDose: number }
export interface TextUsageReportItem { text: string, uses: number }
export interface ReportsResponse {
  fromUtc: string
  toUtc: string
  treatments: { total: number, withPrescription: number, byDay: DailyTreatmentReportItem[], mostVisitedPatients: PatientVisitReportItem[] }
  medications: MedicationReportItem[]
  patients: PatientReportItem[]
  clinicalNotes: TextUsageReportItem[]
  additionalInstructions: TextUsageReportItem[]
  diagnostics: TextUsageReportItem[]
  investigations: TextUsageReportItem[]
  diagnosticsAndInvestigations?: TextUsageReportItem[]
}
