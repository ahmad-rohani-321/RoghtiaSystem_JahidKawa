export default defineEventHandler((event) => {
  assertAuthMutation(event)
  clearAuthCookie(event)
  return { success: true }
})
