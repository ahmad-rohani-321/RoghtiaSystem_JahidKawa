<script setup lang="ts">
const route = useRoute()
const compact = ref(false)
const open = ref(false)
let compactQuery: MediaQueryList | undefined
const items = [
  { to: '/', label: 'Dashboard', compactLabel: 'Home', icon: 'i-lucide-layout-dashboard' },
  { to: '/prescriptions', label: 'Prescriptions', compactLabel: 'Rx', icon: 'i-lucide-clipboard-plus' },
  { to: '/patients', label: 'Patients', compactLabel: 'Patients', icon: 'i-lucide-users-round' },
  { to: '/treatments', label: 'Treatments', compactLabel: 'Care', icon: 'i-lucide-stethoscope' },
  { to: '/medications', label: 'Medications', compactLabel: 'Meds', icon: 'i-lucide-pill' },
  { to: '/reports', label: 'Reports', compactLabel: 'Reports', icon: 'i-lucide-chart-no-axes-combined' },
  { to: '/settings', label: 'Settings', compactLabel: 'Settings', icon: 'i-lucide-settings-2' }
]
const isActive = (to: string) => to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
const activeItem = computed(() => items.find(item => isActive(item.to)))
function updateCompact(event: MediaQueryList | MediaQueryListEvent) {
  compact.value = event.matches
  if (!event.matches) open.value = false
}

onMounted(() => {
  compactQuery = window.matchMedia('(max-width: 1100px)')
  updateCompact(compactQuery)
  compactQuery.addEventListener('change', updateCompact)
})
onBeforeUnmount(() => compactQuery?.removeEventListener('change', updateCompact))
watch(() => route.path, () => { if (compact.value) open.value = false })
</script>

<template>
  <div class="navigation-shell" :class="{ 'is-compact': compact, 'is-open': open }" dir="ltr">
    <button
      v-if="compact"
      type="button"
      class="nav-toggle"
      aria-controls="main-navigation"
      :aria-expanded="open"
      @click="open = !open"
    >
      <span class="nav-toggle-leading"><UIcon :name="open ? 'i-lucide-x' : 'i-lucide-menu'" aria-hidden="true" /><span>Menu</span></span>
      <span class="menu-current"><UIcon :name="activeItem?.icon || 'i-lucide-layout-dashboard'" aria-hidden="true" />{{ activeItem?.compactLabel || 'Home' }}</span>
      <UIcon class="nav-toggle-chevron" :name="open ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" aria-hidden="true" />
    </button>
    <nav id="main-navigation" v-show="!compact || open" class="bottom-nav" aria-label="Main navigation">
      <NuxtLink
        v-for="item in items"
        :key="item.to"
        :to="item.to"
        class="bottom-nav-link"
        :class="{ active: isActive(item.to) }"
        :aria-current="isActive(item.to) ? 'page' : undefined"
        :aria-label="item.label"
        @click="open = false"
      >
        <UIcon :name="item.icon" aria-hidden="true" />
        <span class="full-label">{{ item.label }}</span>
        <span class="compact-label" aria-hidden="true">{{ item.compactLabel }}</span>
      </NuxtLink>
    </nav>
  </div>
</template>

<style scoped>
.navigation-shell { position: relative; flex: 1 1 auto; min-width: 0; max-width: 850px; }
.nav-toggle { display: none; }
.bottom-nav {
  --nav-text: #526f7b;
  --nav-active: #087f8c;
  --nav-active-background: #e3f7f5;
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 4px;
  margin: 0;
  overflow-x: auto;
  scrollbar-width: none;
}
.bottom-nav::-webkit-scrollbar { display: none; }
.bottom-nav :global(.bottom-nav-link) { flex: 1 1 auto; }
.bottom-nav { --nav-border: #d9e9ed; }
:global(.dark) .bottom-nav { --nav-text: #adc5ce; --nav-active: #75e0dd; --nav-active-background: #234448; --nav-border: var(--line); }
.bottom-nav :global(.bottom-nav-link) {
  min-width: max-content;
  min-height: 46px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 8px 10px;
  color: var(--nav-text);
  border-radius: 13px;
  font-size: 0.875rem;
  font-weight: 500;
  line-height: 1.25;
  text-decoration: none;
  transition: background-color .2s ease, color .2s ease, box-shadow .2s ease, transform .15s ease;
}
.bottom-nav :global(.bottom-nav-link > .iconify) { width: 18px; height: 18px; flex-shrink: 0; }
.bottom-nav :global(.bottom-nav-link > span) { min-width: 0; white-space: nowrap; }
.bottom-nav :global(.compact-label) { display: none; }
@media (hover: hover) {
  .bottom-nav :global(.bottom-nav-link:hover) { background: var(--canvas); color: var(--nav-active); }
}
.bottom-nav :global(.bottom-nav-link.active) { color: var(--nav-active); background: var(--nav-active-background); font-weight: 650; box-shadow: inset 0 0 0 1px #087f8c12; }
.bottom-nav :global(.bottom-nav-link:active) { transform: scale(.97); }
.bottom-nav :global(.bottom-nav-link:focus-visible) { outline: 2px solid var(--nav-active); outline-offset: 2px; }
@media (max-width: 1500px) {
  .bottom-nav { justify-content: space-between; margin: 0; padding: 5px; background: var(--canvas); border: 1px solid var(--nav-border); border-radius: 15px; }
  .bottom-nav :global(.bottom-nav-link) { flex: 1 1 0; min-width: 0; min-height: 48px; flex-direction: column; gap: 3px; padding: 4px 3px; font-size: 0.75rem; text-align: center; }
  .bottom-nav :global(.bottom-nav-link:first-child), .bottom-nav :global(.bottom-nav-link:last-child) { margin: 0; }
  .bottom-nav :global(.bottom-nav-link > span) { max-width: 100%; overflow: hidden; text-overflow: ellipsis; }
}
@media (max-width: 1100px) {
  .navigation-shell { flex-basis: 100%; max-width: none; }
  .nav-toggle { width: 100%; min-height: 46px; display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 8px 13px; border: 1px solid var(--nav-border); border-radius: 13px; background: var(--canvas); color: var(--ink); font: inherit; text-align: start; }
  .nav-toggle-leading, .menu-current { display: inline-flex; align-items: center; gap: 9px; min-width: 0; }
  .nav-toggle-leading { color: var(--ink); font-size: .9rem; font-weight: 650; }
  .nav-toggle-leading .iconify { width: 19px; height: 19px; color: var(--teal); }
  .menu-current { margin-inline-start: auto; color: var(--muted); font-size: .84rem; }
  .menu-current .iconify { width: 17px; height: 17px; color: var(--teal); }
  .nav-toggle-chevron { width: 17px; height: 17px; flex: 0 0 auto; color: var(--muted); transition: transform .2s ease; }
  .is-open .nav-toggle { border-color: var(--teal); border-end-start-radius: 8px; border-end-end-radius: 8px; }
  .is-open .bottom-nav { margin-top: 6px; animation: menu-reveal .18s ease-out; }
}
@media (max-width: 700px) {
  .bottom-nav {
    display: grid;
    grid-template-columns: repeat(8, minmax(0, 1fr));
    gap: 4px;
    overflow: visible;
    padding: 6px;
  }
  .bottom-nav :global(.bottom-nav-link) { grid-column: span 2; min-height: 50px; }
  .bottom-nav :global(.bottom-nav-link > span) { white-space: normal; line-height: 1.1; overflow-wrap: anywhere; }
  .bottom-nav :global(.bottom-nav-link:nth-child(5)) { grid-column: 2 / span 2; }
  .bottom-nav :global(.bottom-nav-link:nth-child(6)) { grid-column: 4 / span 2; }
  .bottom-nav :global(.bottom-nav-link:nth-child(7)) { grid-column: 6 / span 2; }
  .bottom-nav :global(.bottom-nav-link > .iconify) { width: 18px; height: 18px; }
}
@keyframes menu-reveal { from { opacity: 0; transform: translateY(-5px); } to { opacity: 1; transform: translateY(0); } }
@media (max-width: 360px) {
  .bottom-nav :global(.bottom-nav-link) { min-height: 48px; font-size: 0.6875rem; }
  .bottom-nav :global(.full-label) { display: none; }
  .bottom-nav :global(.compact-label) { display: inline; }
}
@media (prefers-reduced-motion: reduce) {
  .bottom-nav :global(.bottom-nav-link), .nav-toggle-chevron { transition: none; }
  .bottom-nav :global(.bottom-nav-link:active) { transform: none; }
  .is-open .bottom-nav { animation: none; }
}
</style>
