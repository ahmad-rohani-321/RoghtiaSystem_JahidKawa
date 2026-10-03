export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  let body: { userName?: unknown, pinCode?: unknown, newPassword?: unknown }
  try { body = JSON.parse((await readLimitedBody(event, 16384)).toString('utf8')) }
  catch { throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } }) }
  if (!body || typeof body.userName !== 'string' || !body.userName.trim() || body.userName.trim().length > 64 ||
      typeof body.pinCode !== 'string' || !/^[0-9]{6,10}$/.test(body.pinCode) ||
      typeof body.newPassword !== 'string' || body.newPassword.length < 8 || body.newPassword.length > 128 || body.newPassword === body.pinCode) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  await requestAuthApi(event, 'reset-password', { body: { userName: body.userName.trim(), pinCode: body.pinCode, newPassword: body.newPassword } })
  clearAuthCookie(event)
  return sendNoContent(event)
})
