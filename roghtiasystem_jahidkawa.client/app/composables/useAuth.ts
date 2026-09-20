import type { AuthCredentials, AuthSession } from '#shared/types/auth'

export function useAuth() {
  const nuxtApp = useNuxtApp()
  const session = useState<AuthSession | null>('auth.session', () => null)
  const initialized = useState('auth.initialized', () => false)
  const requestFetch = useRequestFetch()
  const responseCookies = import.meta.server ? useResponseHeader('set-cookie') : null
  const pending = ref(false)

  function clearDoctorInformation() {
    // refresh can resume outside the SSR setup context after its API request.
    nuxtApp.runWithContext(() => clearNuxtState('doctor.information'))
  }

  async function refresh() {
    try {
      session.value = await requestFetch<AuthSession>('/api/auth/me', { retry: 0 })
    } catch (error: unknown) {
      if ((error as { statusCode?: number }).statusCode !== 401) throw error
      // Forward expiry cleanup from the internal SSR request to the browser.
      const cookies = (error as { response?: { headers: Headers } }).response?.headers.getSetCookie()
      if (responseCookies && cookies?.length) responseCookies.value = cookies
      session.value = null
      clearDoctorInformation()
    }
    initialized.value = true
    return session.value
  }

  async function authenticate(mode: 'login' | 'register', credentials: AuthCredentials) {
    if (pending.value) return
    pending.value = true
    try {
      clearDoctorInformation()
      session.value = await $fetch<AuthSession>(`/api/auth/${mode}`, {
        method: 'POST',
        body: credentials,
        headers: { 'x-roghtia-request': '1' },
        retry: 0
      })
      initialized.value = true
    } finally {
      pending.value = false
    }
  }

  async function logout() {
    await $fetch('/api/auth/logout', { method: 'POST', headers: { 'x-roghtia-request': '1' }, retry: 0 })
    session.value = null
    clearDoctorInformation()
    initialized.value = true
    return navigateTo('/login')
  }

  const user = computed(() => session.value?.user ?? null)
  return {
    user,
    expiresAt: computed(() => session.value?.expiresAt ?? null),
    isAuthenticated: computed(() => !!user.value),
    initialized: readonly(initialized),
    pending: readonly(pending),
    refresh,
    authenticate,
    logout,
    // UI helpers only. The API remains responsible for authorization.
    hasRole: (role: string) => user.value?.roles.includes(role) ?? false,
    hasPermission: (permission: string) => user.value?.permissions.includes(permission) ?? false
  }
}
