import type { AuthUser } from '../../../shared/types/auth'

export default defineEventHandler(async (event) => {
  const token = requireAuthToken(event)
  const user = await requestAuthApi<AuthUser>(event, 'me', { token })
  return { user, expiresAt: getVerifiedTokenExpiry(token) }
})
