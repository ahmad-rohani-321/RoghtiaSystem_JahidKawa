export interface ClinicalNote { id: number, text: string }
export interface PrescriptionItemInput { medicationId: number, duration: string, refills: number }
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
  clinicalNoteIds: number[]
  prescriptionItems: PrescriptionItemInput[]
}
export interface TreatmentSummary {
  id: number
  recordedAtUtc: string
  patient: TreatmentPatient
  bloodPressure: string
  medicationCount: number
  clinicalNoteCount: number
}
export interface Treatment {
  id: number
  recordedAtUtc: string
  patient: TreatmentPatient
  bloodPressure: string
  clinicalNotes: ClinicalNote[]
  prescriptionItems: PrescriptionItem[]
}
export interface TreatmentPage { items: TreatmentSummary[], total: number, page: number, pageSize: number }
export interface TreatmentQuery {
  page?: number
  pageSize?: number
  patientName?: string
  fromUtc?: string
  toUtc?: string
  hasPrescription?: boolean
}
