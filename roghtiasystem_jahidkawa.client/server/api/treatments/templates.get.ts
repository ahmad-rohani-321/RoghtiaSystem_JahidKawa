import type { TreatmentTemplate } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<TreatmentTemplate[]>(event, '/api/treatments/templates'))._data
})
