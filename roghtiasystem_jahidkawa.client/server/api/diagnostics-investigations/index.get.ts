import type { DiagnosticInvestigation } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  const kind = getQuery(event).kind
  if (kind !== 'Diagnostic' && kind !== 'Investigation') throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  return (await requestOwnedApi<DiagnosticInvestigation[]>(event, '/api/diagnostics-investigations', { query: { kind } }))._data
})
