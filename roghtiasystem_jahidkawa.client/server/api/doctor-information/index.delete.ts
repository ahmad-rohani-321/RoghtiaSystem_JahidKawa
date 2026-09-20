export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  await requestOwnedApi(event, '/api/doctor-information', { method: 'DELETE' })
  return sendNoContent(event)
})
