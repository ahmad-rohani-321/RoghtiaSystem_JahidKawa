export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  requireAuthToken(event)
  let body: { currentPassword?: unknown, newPassword?: unknown }
  try { body = JSON.parse((await readLimitedBody(event, 16384)).toString('utf8')) }
  catch { throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } }) }
  if (!body || typeof body.currentPassword !== 'string' || typeof body.newPassword !== 'string' ||
      !body.currentPassword || body.currentPassword.length > 128 || body.newPassword.length < 8 ||
      body.newPassword.length > 128 || body.currentPassword === body.newPassword) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  await requestOwnedApi(event, '/api/auth/change-password', { method: 'POST', body: { currentPassword: body.currentPassword, newPassword: body.newPassword } })
  clearAuthCookie(event)
  return sendNoContent(event)
})
