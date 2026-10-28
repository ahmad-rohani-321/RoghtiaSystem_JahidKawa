export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  const body = await readBody<{ printOnA5?: unknown, printGrayscale?: unknown }>(event)
  if (typeof body?.printOnA5 !== 'boolean' || typeof body?.printGrayscale !== 'boolean') {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  return (await requestOwnedApi(event, '/api/doctor-information/print-preferences', {
    method: 'PUT', body: { printOnA5: body.printOnA5, printGrayscale: body.printGrayscale }, contentType: 'application/json'
  }))._data
})
