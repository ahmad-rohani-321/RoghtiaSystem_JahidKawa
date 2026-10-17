<script setup>
const colorMode = useColorMode()
const auth = useAuth()
const toast = useToast()
const doctor = useDoctorInformation()
const passwordOpen = ref(false)
const pinOpen = ref(false)
const username = computed(() => auth.user.value?.userName || '')
const displayName = computed(() => doctor.information.value?.doctorNameEnglish || username.value)
const initials = computed(() => displayName.value.slice(0, 2))
// Profile outages must not prevent access to the rest of the application.
try { await doctor.load() } catch { /* Settings presents a retry action. */ }
async function logout() {
  try { await auth.logout() }
  catch { toast.add({ title: 'Unable to sign out', description: 'Please try again.', color: 'error' }) }
}
const menu = computed(() => [[{ type: 'label', label: displayName.value, description: username.value }], [{ label: 'Settings', icon: 'i-lucide-settings-2', to: '/settings' }, { label: 'Change password', icon: 'i-lucide-key-round', onSelect: () => { passwordOpen.value = true } }, { label: 'Reset PIN code', icon: 'i-lucide-shield-check', onSelect: () => { pinOpen.value = true } }], [{ label: 'Sign out', icon: 'i-lucide-log-out', onSelect: logout }]])
</script>
<template>
  <div class="app-shell">
    <a href="#main" class="skip-link">Skip to main content</a>
    <header class="topbar">
      <NuxtLink to="/" class="brand"><span class="brand-icon"><UIcon name="i-lucide-heart-pulse" /></span><span><strong>E-Roghtia<span class="brand-dot">.</span></strong><small>Your partner in care</small></span></NuxtLink>
      <BottomNavigation />
      <div class="header-actions">
        <PwaInstallPrompt />
        <UTooltip text="Toggle color theme"><UButton :icon="colorMode.value === 'dark' ? 'i-lucide-sun' : 'i-lucide-moon'" color="neutral" variant="ghost" aria-label="Toggle color theme" @click="colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'" /></UTooltip>
        <UDropdownMenu :items="menu" :ui="{ content: 'w-64 max-w-[calc(100vw-1.5rem)]', itemLabel: 'whitespace-normal break-words', itemDescription: 'break-all' }"><button type="button" class="profile" aria-label="Account options"><UAvatar :src="doctor.information.value?.doctorPhoto || undefined" :alt="displayName" :text="initials" class="avatar" :ui="{ image: 'size-full object-cover', fallback: 'text-base' }" /><span class="profile-copy"><strong>{{ displayName }}</strong><small dir="auto">{{ username }}</small></span><UIcon name="i-lucide-chevron-down" /></button></UDropdownMenu>
      </div>
    </header>
    <main id="main" class="main-container"><slot /></main>
    <PasswordChangeModal v-model:open="passwordOpen" />
    <PINCodeModal v-model:open="pinOpen" />
  </div>
</template>

<style scoped>
.profile-copy { min-width: 0; max-width: 200px; }
.profile-copy strong, .profile-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.profile .avatar { flex-shrink: 0; }
.app-shell .main-container { padding-bottom: 35px; }
.app-shell .topbar { position: sticky; top: 0; z-index: 40; height: auto; min-height: 82px; flex-wrap: nowrap; gap: clamp(14px, 2vw, 28px); padding: 12px max(28px, 4.5%); background: color-mix(in srgb, var(--surface) 92%, transparent); backdrop-filter: blur(16px); }
.app-shell .brand { flex: 0 0 auto; }
.app-shell :deep(.navigation-shell) { flex: 1 1 auto; max-width: 850px; min-width: 0; margin: 0; }
.app-shell :deep(.bottom-nav) { padding: 5px; gap: 3px; border: 1px solid var(--line); border-radius: 16px; background: var(--canvas); }
.app-shell :deep(.bottom-nav-link) { padding-inline: clamp(8px, .8vw, 13px); }
.app-shell .header-actions { flex: 0 0 auto; gap: 8px; }
.app-shell .profile { min-height: 48px; max-width: 230px; padding: 4px 9px; border: 1px solid transparent; border-radius: 14px; transition: background .2s ease, border-color .2s ease; }
.app-shell .profile:hover { background: var(--canvas); border-color: var(--line); }
@media (max-width: 1500px) {
  .app-shell .topbar { min-height: 0; flex-wrap: wrap; row-gap: 12px; }
  .app-shell .brand { order: 1; }
  .app-shell .header-actions { order: 2; }
  .app-shell :deep(.navigation-shell) { order: 3; flex: 0 0 100%; max-width: none; }
}
@media (max-width: 700px) {
  .app-shell .topbar { gap: 9px; padding: max(10px, env(safe-area-inset-top, 0px)) max(12px, env(safe-area-inset-right, 0px)) 10px max(12px, env(safe-area-inset-left, 0px)); }
  .app-shell .brand { flex: 1 1 auto; min-width: 0; gap: 8px; }
  .app-shell .brand strong { font-size: 1.45rem; }
  .app-shell .brand small { margin-top: 2px; font-size: .75rem; }
  .app-shell .brand-icon { width: 40px; height: 40px; border-radius: 13px; }
  .app-shell .header-actions { min-width: 0; gap: 3px; }
  .app-shell :deep(.bottom-nav) { padding: 5px; border-radius: 14px; }
  .app-shell .profile { padding: 3px; }
}
@media (max-width: 380px) {
  .app-shell .brand small { display: none; }
  .app-shell .brand strong { font-size: 1.3rem; }
  .app-shell .brand-icon { width: 36px; height: 36px; }
}
</style>
