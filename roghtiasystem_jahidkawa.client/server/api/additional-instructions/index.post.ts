import type { AdditionalInstruction } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  const body = await readBody<{ text?: unknown }>(event)
  if (typeof body?.text !== 'string' || !body.text.trim() || body.text.length > 300) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  return (await requestOwnedApi<AdditionalInstruction>(event, '/api/additional-instructions', { method: 'POST', body: { text: body.text.trim() } }))._data
})
