export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  const credentials = readAuthCredentials(await readBody(event), true)
  const response = await requestAuthApi<import('../../../shared/types/auth').AuthTokenResponse>(event, 'register', { body: credentials })
  return establishAuthSession(event, response, false)
})
