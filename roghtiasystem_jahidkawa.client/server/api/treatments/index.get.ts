import type { TreatmentPage } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<TreatmentPage>(event, '/api/treatments', { query: readTreatmentQuery(event) }))._data
})
