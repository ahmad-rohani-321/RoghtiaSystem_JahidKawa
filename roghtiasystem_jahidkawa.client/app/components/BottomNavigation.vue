<script setup lang="ts">
const route = useRoute()
const items = [
  { to: '/', label: 'لنډیز', icon: 'i-lucide-layout-dashboard' },
  { to: '/prescriptions', label: 'نسخې', icon: 'i-lucide-clipboard-plus' },
  { to: '/patients', label: 'ناروغان', icon: 'i-lucide-users-round' },
  { to: '/treatments', label: 'تداوي', icon: 'i-lucide-stethoscope' },
  { to: '/medications', label: 'درمل', icon: 'i-lucide-pill' },
  { to: '/reports', label: 'راپورونه', icon: 'i-lucide-chart-no-axes-combined' },
  { to: '/settings', label: 'تنظیمات', icon: 'i-lucide-settings-2' }
]
const isActive = (to: string) => to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
</script>

<template>
  <nav class="bottom-nav" dir="rtl" aria-label="اصلي لارښود">
    <NuxtLink
      v-for="item in items"
      :key="item.to"
      :to="item.to"
      class="bottom-nav-link"
      :class="{ active: isActive(item.to) }"
      :aria-current="isActive(item.to) ? 'page' : undefined"
    >
      <UIcon :name="item.icon" aria-hidden="true" />
      <span>{{ item.label }}</span>
    </NuxtLink>
  </nav>
</template>

<style scoped>
.bottom-nav {
  --nav-text: #526f7b;
  --nav-active: #087f8c;
  --nav-active-background: #dff7f7;
  position: fixed;
  z-index: 40;
  inset-inline: 0;
  bottom: calc(22px + env(safe-area-inset-bottom, 0px));
  width: min(930px, calc(100% - 32px - env(safe-area-inset-left, 0px) - env(safe-area-inset-right, 0px)));
  margin-inline: auto;
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 8px;
  padding: 9px;
  background: var(--surface);
  border: 1px solid #d9e9ed;
  border-radius: 21px;
  box-shadow: 0 8px 32px #2b72800d;
}
.bottom-nav-link {
  min-width: 0;
  min-height: 52px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 10px 8px;
  color: var(--nav-text);
  border-radius: 13px;
  font-size: 1rem;
  font-weight: 450;
  line-height: 1.5;
  text-decoration: none;
  transition: background-color .2s ease, color .2s ease, transform .15s ease;
}
.bottom-nav-link > .iconify { width: 21px; height: 21px; flex-shrink: 0; }
.bottom-nav-link > span { min-width: 0; white-space: nowrap; }
@media (hover: hover) {
  .bottom-nav-link:hover { background: var(--canvas); color: var(--nav-active); }
}
.bottom-nav-link.active { color: var(--nav-active); background: var(--nav-active-background); font-weight: 600; }
.bottom-nav-link:active { transform: scale(.97); }
.bottom-nav-link:focus-visible { outline: 2px solid var(--nav-active); outline-offset: 2px; }
:global(.dark .bottom-nav) {
  --nav-text: #adc5ce;
  --nav-active: #75e0dd;
  --nav-active-background: #234448;
  border-color: var(--line);
  box-shadow: 0 8px 32px #0003;
}
@media (max-width: 860px) {
  .bottom-nav { grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 6px; bottom: calc(12px + env(safe-area-inset-bottom, 0px)); }
  .bottom-nav-link { gap: 7px; padding-inline: 6px; }
}
@media (max-width: 380px) {
  .bottom-nav { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .bottom-nav-link { min-height: 46px; }
}
@media (prefers-reduced-motion: reduce) {
  .bottom-nav-link { transition: none; }
  .bottom-nav-link:active { transform: none; }
}
</style>
