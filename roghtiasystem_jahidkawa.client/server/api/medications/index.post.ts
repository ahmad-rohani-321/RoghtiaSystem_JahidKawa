import type { Medication } from '../../../shared/types/medication'

export default defineEventHandler(async (event) => {
  const input = await readMedicationInput(event)
  const response = await requestOwnedApi<Medication>(event, '/api/medications', { method: 'POST', body: { ...input } })
  setResponseStatus(event, 201)
  return response._data
})
