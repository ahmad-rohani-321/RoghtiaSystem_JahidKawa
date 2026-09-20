import type { Medication } from '../../../shared/types/medication'

export default defineEventHandler(async (event) => {
  const input = await readMedicationInput(event)
  return (await requestOwnedApi<Medication>(event, `/api/medications/${readMedicationId(event)}`, { method: 'PUT', body: { ...input } }))._data
})
