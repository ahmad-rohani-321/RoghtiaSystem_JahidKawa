import type { Treatment } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  const input = await readTreatmentInput(event)
  const response = await requestOwnedApi<Treatment>(event, '/api/treatments', { method: 'POST', body: { ...input } })
  setResponseStatus(event, 201)
  return response._data
})
