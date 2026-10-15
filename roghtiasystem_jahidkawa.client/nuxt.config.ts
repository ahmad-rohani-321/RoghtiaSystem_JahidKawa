export default defineNuxtConfig({
  compatibilityDate: '2026-09-01',
  modules: ['@nuxt/ui'],
  ui: { fonts: false },
  css: ['~/assets/css/main.css'],
  devtools: { enabled: false },
  runtimeConfig: {
    authApiBase: 'http://localhost:5201',
    authCookieSecure: process.env.NODE_ENV === 'production'
  },
  app: { head: { htmlAttrs: { lang: 'en', dir: 'ltr' }, title: 'Roghtia — Healthcare Management', meta: [{ name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' }, { name: 'description', content: 'Roghtia — Patient care and healthcare management' }] } },
  colorMode: { preference: 'light', fallback: 'light' },
  icon: { clientBundle: { scan: true } }
})
