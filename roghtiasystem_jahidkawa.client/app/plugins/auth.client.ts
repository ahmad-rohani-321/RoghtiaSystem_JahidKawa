export default defineNuxtPlugin(() => {
  const auth = useAuth()
  const route = useRoute()
  let expiryTimer: ReturnType<typeof setTimeout> | undefined
  let refreshing = false

  async function verifySession() {
    if (!auth.isAuthenticated.value || refreshing) return
    refreshing = true
    try {
      const session = await auth.refresh()
      if (!session && !['/login', '/register'].includes(route.path)) await navigateTo('/login')
    } catch {
      // A temporary network failure is not proof that the session has expired.
    } finally {
      refreshing = false
      scheduleExpiryCheck()
    }
  }

  function scheduleExpiryCheck() {
    clearTimeout(expiryTimer)
    if (!auth.expiresAt.value) return
    const remaining = Date.parse(auth.expiresAt.value) - Date.now()
    expiryTimer = setTimeout(verifySession, Math.min(Math.max(remaining + 1000, 30000), 2147483647))
  }

  watch(auth.expiresAt, scheduleExpiryCheck, { immediate: true })
  window.addEventListener('focus', verifySession)
  if (import.meta.hot) import.meta.hot.dispose(() => {
    clearTimeout(expiryTimer)
    window.removeEventListener('focus', verifySession)
  })
})
