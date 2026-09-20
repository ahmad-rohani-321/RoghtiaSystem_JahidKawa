export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  return (await requestOwnedApi(event, '/api/doctor-information/reset', { method: 'POST' }))._data
})
