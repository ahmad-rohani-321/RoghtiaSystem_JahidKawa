export interface PatientInput {
  name: string
  phone: string
  age: number
  gender: string
  address: string
}

export interface Patient extends PatientInput {
  code: string
  id: number
  user: { id: number, userName: string }
}

export interface PatientPage {
  items: Patient[]
  total: number
  page: number
  pageSize: number
}

export interface PatientQuery {
  page?: number
  pageSize?: number
  search?: string
}
