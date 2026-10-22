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
  app: { head: { htmlAttrs: { lang: 'en', dir: 'ltr' }, title: 'E-Roghtia — Healthcare Management', link: [{ rel: 'manifest', href: '/manifest.webmanifest' }, { rel: 'apple-touch-icon', href: '/icons/icon-192.png' }], meta: [{ name: 'theme-color', content: '#0d9488' }, { name: 'mobile-web-app-capable', content: 'yes' }, { name: 'apple-mobile-web-app-capable', content: 'yes' }, { name: 'apple-mobile-web-app-status-bar-style', content: 'default' }, { name: 'apple-mobile-web-app-title', content: 'E-Roghtia' }, { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' }, { name: 'description', content: 'E-Roghtia — Patient care and healthcare management' }] } },
  colorMode: { preference: 'light', fallback: 'light' },
  icon: { clientBundle: { scan: true } }
})
