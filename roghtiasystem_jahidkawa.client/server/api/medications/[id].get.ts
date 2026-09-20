import type { Medication } from '../../../shared/types/medication'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<Medication>(event, `/api/medications/${readMedicationId(event)}`))._data
})
