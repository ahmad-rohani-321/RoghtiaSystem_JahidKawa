import type { Medication } from '../../../shared/types/medication'

export default defineEventHandler(async (event) => {
  const items = await readBulkMedicationInput(event)
  const response = await requestOwnedApi<Medication[]>(event, '/api/medications/bulk', { method: 'POST', body: { items } })
  return response._data
})
