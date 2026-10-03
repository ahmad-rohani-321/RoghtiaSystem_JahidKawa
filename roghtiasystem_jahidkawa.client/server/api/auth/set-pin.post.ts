export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  requireAuthToken(event)
  let body: { currentPassword?: unknown, pinCode?: unknown }
  try { body = JSON.parse((await readLimitedBody(event, 16384)).toString('utf8')) }
  catch { throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } }) }
  if (!body || typeof body.currentPassword !== 'string' || !body.currentPassword || body.currentPassword.length > 128 ||
      typeof body.pinCode !== 'string' || !/^[0-9]{6,10}$/.test(body.pinCode)) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  await requestOwnedApi(event, '/api/auth/set-pin', { method: 'POST', body: { currentPassword: body.currentPassword, pinCode: body.pinCode } })
  return sendNoContent(event)
})
