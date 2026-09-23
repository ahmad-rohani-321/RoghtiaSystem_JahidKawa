import type { Patient } from '../../../shared/types/patient'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<Patient>(event, `/api/patients/${readPatientId(event)}`))._data
})
