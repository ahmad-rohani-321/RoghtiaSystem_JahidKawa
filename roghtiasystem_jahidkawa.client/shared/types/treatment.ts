export interface ClinicalNote { id: number, text: string }
export interface AdditionalInstruction { id: number, text: string }
export const prescriptionFrequencies = ['Once daily', 'Twice daily', 'Three times daily', 'Four times daily', 'Every 4 hours', 'Every 6 hours', 'Every 8 hours', 'Every 12 hours', 'Every 24 hours', 'As needed', 'Immediately', 'Single dose', 'At bedtime', 'Every morning', 'Every evening', 'Before meals', 'After meals', 'Before breakfast', 'After breakfast', 'Before lunch', 'After lunch', 'Before dinner', 'After dinner', 'With meals', 'Every other day', 'Weekly', 'Twice weekly', 'Monthly'] as const
export const prescriptionFrequencyCodes: Partial<Record<typeof prescriptionFrequencies[number], string>> = {
  'Once daily': 'QD', 'Twice daily': 'BID', 'Three times daily': 'TID', 'Four times daily': 'QID',
  'Every 4 hours': 'Q4H', 'Every 6 hours': 'Q6H', 'Every 8 hours': 'Q8H', 'Every 12 hours': 'Q12H',
  'Every 24 hours': 'Q24H', 'As needed': 'PRN', Immediately: 'STAT', 'Single dose': 'Once',
  'At bedtime': 'HS', 'Every morning': 'QAM', 'Every evening': 'QPM', 'Before meals': 'AC', 'After meals': 'PC'
}
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
  saveAsTemplate?: boolean
  templateId?: number | null
  templateName?: string
}
export interface TreatmentTemplate {
  id: number
  name: string
  bloodPressure: string
  additionalInstructions: string
  clinicalNoteIds: number[]
  additionalInstructionIds: number[]
  prescriptionItems: PrescriptionItemInput[]
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
