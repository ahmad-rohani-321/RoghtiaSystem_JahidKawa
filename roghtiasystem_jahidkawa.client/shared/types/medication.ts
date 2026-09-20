export interface MedicationInput {
  name: string
  type: string
  quantity: number
  remarks: string
}

export interface Medication extends MedicationInput {
  id: number
  user: { id: number, userName: string }
}

export interface MedicationPage {
  items: Medication[]
  total: number
  page: number
  pageSize: number
}

export interface MedicationQuery {
  page?: number
  pageSize?: number
  search?: string
}
