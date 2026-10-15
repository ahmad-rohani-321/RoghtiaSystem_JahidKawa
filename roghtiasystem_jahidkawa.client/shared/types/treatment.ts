export interface ClinicalNote { id: number, text: string }
export interface AdditionalInstruction { id: number, text: string }
export const prescriptionFrequencies = ['Once daily', 'Twice daily', 'Three times daily', 'Four times daily', 'Every 4 hours', 'Every 6 hours', 'Every 8 hours', 'Every 12 hours', 'Every 24 hours', 'Once weekly', 'As needed'] as const
export interface PrescriptionItemInput { medicationId: number, dose: number, frequency: string, duration: string }
export interface PrescriptionItem extends PrescriptionItemInput {
  id: number
  position: number
  medicationName: string
  medicationType: string
}
export interface TreatmentPatient {
  id: number
  code: string
  name: string
  phone: string
  age: number
  gender: string
  address: string
}
export interface TreatmentInput {
  patientId: number
  bloodPressure: string
  additionalInstructions: string
  additionalInstructionIds: number[]
  clinicalNoteIds: number[]
  prescriptionItems: PrescriptionItemInput[]
  allowInsufficientStock?: boolean
}
export interface TreatmentSummary {
  id: number
  recordedAtUtc: string
  patient: TreatmentPatient
  bloodPressure: string
  additionalInstructions: string
  medicationCount: number
  clinicalNoteCount: number
}
export interface Treatment {
  id: number
  recordedAtUtc: string
  patient: TreatmentPatient
  bloodPressure: string
  additionalInstructions: string
  clinicalNotes: ClinicalNote[]
  additionalInstructionItems: AdditionalInstruction[]
  prescriptionItems: PrescriptionItem[]
}
export interface TreatmentPage { items: TreatmentSummary[], total: number, page: number, pageSize: number }
export interface DashboardSummary {
  totalPatients: number
  patientsToday: number
  treatmentsToday: number
  prescriptionsToday: number
  totalMedications: number
  visits: { date: string, visits: number, prescriptions: number }[]
  recentTreatments: TreatmentSummary[]
}
export interface TreatmentQuery {
  page?: number
  pageSize?: number
  patientName?: string
  fromUtc?: string
  toUtc?: string
  hasPrescription?: boolean
}
