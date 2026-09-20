export default defineEventHandler((event) => {
  // Authenticated HTML must never be reused for a different user.
  if (!event.path.startsWith('/_nuxt/') && !event.path.startsWith('/__nuxt')) {
    setHeader(event, 'Cache-Control', 'no-store')
  }
})
