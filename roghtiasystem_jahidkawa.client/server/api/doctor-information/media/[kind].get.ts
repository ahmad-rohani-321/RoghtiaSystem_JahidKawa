export default defineEventHandler(async (event) => {
  const kind = getRouterParam(event, 'kind')
  if (!['doctor-photo', 'doctor-logo', 'hospital-logo'].includes(kind || '')) {
    throw createError({ statusCode: 404, data: { code: 'NOT_FOUND' } })
  }
  const response = await requestOwnedApi<ArrayBuffer>(event, `/api/doctor-information/media/${kind}`, { binary: true })
  setHeader(event, 'Content-Type', response.headers.get('content-type') || 'application/octet-stream')
  setHeader(event, 'X-Content-Type-Options', 'nosniff')
  setHeader(event, 'Content-Security-Policy', "default-src 'none'; sandbox")
  return send(event, Buffer.from(response._data!))
})
