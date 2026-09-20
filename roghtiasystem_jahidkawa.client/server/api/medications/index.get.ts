import type { MedicationPage } from '../../../shared/types/medication'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<MedicationPage>(event, '/api/medications', { query: readMedicationQuery(event) }))._data
})
