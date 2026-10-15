import type { DashboardSummary } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<DashboardSummary>(event, '/api/treatments/dashboard'))._data
})
