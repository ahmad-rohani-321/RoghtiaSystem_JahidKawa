import type { Treatment } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  const id = readTreatmentId(event)
  const input = await readTreatmentInput(event)
  return (await requestOwnedApi<Treatment>(event, `/api/treatments/${id}`, { method: 'PUT', body: { ...input } }))._data
})
