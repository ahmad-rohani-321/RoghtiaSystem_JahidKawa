import type { ReportsResponse } from '../../shared/types/reports'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const fromUtc = query.fromUtc
  const toUtc = query.toUtc
  const isoUtc = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/
  if (typeof fromUtc !== 'string' || typeof toUtc !== 'string' || !isoUtc.test(fromUtc) || !isoUtc.test(toUtc) ||
      !Number.isFinite(Date.parse(fromUtc)) || !Number.isFinite(Date.parse(toUtc)) || Date.parse(fromUtc) >= Date.parse(toUtc) ||
      Date.parse(toUtc) - Date.parse(fromUtc) > 367 * 86400000) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  const response = await requestOwnedApi<ReportsResponse>(event, '/api/reports', { query: { fromUtc, toUtc } })
  return response._data
})
