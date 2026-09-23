import type { PatientPage } from '../../../shared/types/patient'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<PatientPage>(event, '/api/patients', { query: readPatientQuery(event) }))._data
})
