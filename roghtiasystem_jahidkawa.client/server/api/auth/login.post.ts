export default defineEventHandler(async (event) => {
  assertAuthMutation(event)
  const credentials = readAuthCredentials(await readBody(event))
  const response = await requestAuthApi<import('../../../shared/types/auth').AuthTokenResponse>(event, 'login', { body: credentials })
  return establishAuthSession(event, response, credentials.rememberMe === true)
})
