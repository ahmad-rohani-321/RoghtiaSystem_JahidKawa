export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  let body: { userName?: unknown, pinCode?: unknown }
  try { body = JSON.parse((await readLimitedBody(event, 16384)).toString('utf8')) }
  catch { throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } }) }
  if (!body || typeof body.userName !== 'string' || !body.userName.trim() || body.userName.trim().length > 64 ||
      typeof body.pinCode !== 'string' || !/^[0-9]{6,10}$/.test(body.pinCode)) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  await requestAuthApi(event, 'verify-pin', { body: { userName: body.userName.trim(), pinCode: body.pinCode } })
  return sendNoContent(event)
})
