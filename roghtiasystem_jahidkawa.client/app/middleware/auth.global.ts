export default defineNuxtRouteMiddleware(async (to) => {
  const auth = useAuth()
  const nuxtApp = useNuxtApp()
  // Reuse the server-verified identity for hydration; check subsequent navigation.
  if (!(import.meta.client && nuxtApp.isHydrating && auth.initialized.value)) {
    try {
      await auth.refresh()
    } catch {
      throw createError({ statusCode: 503, statusMessage: 'د ننوتلو خدمت ته لاسرسی نشته. بیا هڅه وکړئ.' })
    }
  }
  const publicPage = ['/login', '/register'].includes(to.path)
  if (!auth.isAuthenticated.value && !publicPage) {
    return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }
  if (auth.isAuthenticated.value && publicPage) return navigateTo('/')
})
