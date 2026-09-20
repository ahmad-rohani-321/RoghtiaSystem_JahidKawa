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
  app: { head: { htmlAttrs: { lang: 'ps', dir: 'rtl' }, title: 'روغتیا — د روغتیا مدیریت', meta: [{ name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' }, { name: 'description', content: 'روغتیا — د ناروغانو او روغتیايي خدمتونو مدیریت' }] } },
  colorMode: { preference: 'light', fallback: 'light' },
  icon: { clientBundle: { scan: true } }
})
