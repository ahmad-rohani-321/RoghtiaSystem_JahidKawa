import type { Treatment } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<Treatment>(event, `/api/treatments/${readTreatmentId(event)}`))._data
})
