import type { DiagnosticInvestigation } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  const body = await readBody<{ text?: unknown, kind?: unknown }>(event)
  if (typeof body?.text !== 'string' || !body.text.trim() || body.text.length > 300 ||
      (body.kind !== 'Diagnostic' && body.kind !== 'Investigation')) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  return (await requestOwnedApi<DiagnosticInvestigation>(event, '/api/diagnostics-investigations', {
    method: 'POST', body: { text: body.text.trim(), kind: body.kind }
  }))._data
})
