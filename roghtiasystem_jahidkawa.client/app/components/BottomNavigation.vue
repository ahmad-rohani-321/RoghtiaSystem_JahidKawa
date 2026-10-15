<script setup lang="ts">
const route = useRoute()
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
</script>

<template>
  <nav class="bottom-nav" dir="ltr" aria-label="Main navigation">
    <NuxtLink
      v-for="item in items"
      :key="item.to"
      :to="item.to"
      class="bottom-nav-link"
      :class="{ active: isActive(item.to) }"
      :aria-current="isActive(item.to) ? 'page' : undefined"
      :aria-label="item.label"
    >
      <UIcon :name="item.icon" aria-hidden="true" />
      <span class="full-label">{{ item.label }}</span>
      <span class="compact-label" aria-hidden="true">{{ item.compactLabel }}</span>
    </NuxtLink>
  </nav>
</template>

<style scoped>
.bottom-nav {
  --nav-text: #526f7b;
  --nav-active: #087f8c;
  --nav-active-background: #e3f7f5;
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  margin-inline: 12px;
  overflow-x: auto;
  scrollbar-width: none;
}
.bottom-nav::-webkit-scrollbar { display: none; }
.bottom-nav :global(.bottom-nav-link) { flex: 0 0 auto; }
.bottom-nav :global(.bottom-nav-link):first-child { margin-inline-start: auto; }
.bottom-nav :global(.bottom-nav-link):last-child { margin-inline-end: auto; }
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
@media (max-width: 1000px) {
  .bottom-nav { justify-content: space-between; margin: 0; padding: 5px; background: var(--canvas); border: 1px solid var(--nav-border); border-radius: 15px; }
  .bottom-nav :global(.bottom-nav-link) { flex: 1 1 0; min-width: 0; min-height: 48px; flex-direction: column; gap: 3px; padding: 4px 3px; font-size: 0.75rem; text-align: center; }
  .bottom-nav :global(.bottom-nav-link:first-child), .bottom-nav :global(.bottom-nav-link:last-child) { margin: 0; }
  .bottom-nav :global(.bottom-nav-link > span) { max-width: 100%; overflow: hidden; text-overflow: ellipsis; }
}
@media (max-width: 700px) {
  .bottom-nav {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 4px;
    overflow: visible;
    padding: 6px;
  }
  .bottom-nav :global(.bottom-nav-link) { min-height: 50px; }
  .bottom-nav :global(.bottom-nav-link > span) { white-space: normal; line-height: 1.1; overflow-wrap: anywhere; }
  .bottom-nav :global(.bottom-nav-link:nth-child(5)) { grid-column-start: 2; }
  .bottom-nav :global(.bottom-nav-link > .iconify) { width: 18px; height: 18px; }
}
@media (max-width: 360px) {
  .bottom-nav :global(.bottom-nav-link) { min-height: 48px; font-size: 0.6875rem; }
  .bottom-nav :global(.full-label) { display: none; }
  .bottom-nav :global(.compact-label) { display: inline; }
}
@media (prefers-reduced-motion: reduce) {
  .bottom-nav :global(.bottom-nav-link) { transition: none; }
  .bottom-nav :global(.bottom-nav-link:active) { transform: none; }
}
</style>
