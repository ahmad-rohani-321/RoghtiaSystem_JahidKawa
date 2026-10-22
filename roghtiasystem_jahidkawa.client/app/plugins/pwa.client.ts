export default defineNuxtPlugin(() => {
  if (!('serviceWorker' in navigator)) return

  // The cache-first PWA worker must not serve stale Vite modules during local development.
  if (import.meta.dev) {
    void (async () => {
      const registrations = await navigator.serviceWorker.getRegistrations()
      const rootScope = new URL('/', window.location.href).href
      const appRegistrations = registrations.filter((registration) => {
        const worker = registration.active || registration.waiting || registration.installing
        if (!worker || registration.scope !== rootScope) return false
        const scriptUrl = new URL(worker.scriptURL)
        return scriptUrl.origin === window.location.origin && scriptUrl.pathname === '/sw.js'
      })
      const controller = navigator.serviceWorker.controller
      const removeController = !!controller && new URL(controller.scriptURL).origin === window.location.origin &&
        new URL(controller.scriptURL).pathname === '/sw.js'
      const removed = await Promise.all(appRegistrations.map(registration => registration.unregister()))

      if ('caches' in window) {
        const names = await window.caches.keys()
        await Promise.all(names.filter(name => name.startsWith('e-roghtia-')).map(name => window.caches.delete(name)))
      }

      const refreshKey = 'e-roghtia-dev-worker-cleared'
      if ((removeController || removed.some(Boolean)) && !window.sessionStorage.getItem(refreshKey)) {
        window.sessionStorage.setItem(refreshKey, '1')
        window.location.reload()
      } else if (!appRegistrations.length) {
        window.sessionStorage.removeItem(refreshKey)
      }
    })().catch((error: unknown) => {
      console.warn('E-Roghtia development cache cleanup failed.', error)
    })
    return
  }

  navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch((error: unknown) => {
    console.warn('E-Roghtia service worker registration failed.', error)
  })
})
