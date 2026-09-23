<script setup>
const colorMode = useColorMode()
const auth = useAuth()
const toast = useToast()
const doctor = useDoctorInformation()
const passwordOpen = ref(false)
const username = computed(() => auth.user.value?.userName || '')
const displayName = computed(() => doctor.information.value?.doctorNamePashto || username.value)
const initials = computed(() => displayName.value.slice(0, 2))
// Profile outages must not prevent access to the rest of the application.
try { await doctor.load() } catch { /* Settings presents a retry action. */ }
async function logout() {
  try { await auth.logout() }
  catch { toast.add({ title: 'وتل ونه شول', description: 'مهرباني وکړئ بیا هڅه وکړئ.', color: 'error' }) }
}
const menu = computed(() => [[{ type: 'label', label: displayName.value, description: username.value }], [{ label: 'تنظیمات', icon: 'i-lucide-settings-2', to: '/settings' }, { label: 'د پټنوم بدلول', icon: 'i-lucide-key-round', onSelect: () => { passwordOpen.value = true } }], [{ label: 'له حسابه وتل', icon: 'i-lucide-log-out', onSelect: logout }]])
</script>
<template>
  <div class="app-shell">
    <a href="#main" class="skip-link">اصلي منځپانګې ته لاړ شئ</a>
    <header class="topbar">
      <NuxtLink to="/" class="brand"><span class="brand-icon"><UIcon name="i-lucide-heart-pulse" /></span><span><strong>روغتیا<span class="brand-dot">.</span></strong><small>ستاسو د پاملرنې ملګری</small></span></NuxtLink>
      <div class="header-center"><span class="live-dot" /> د روغتیا مدیریت سیسټم <span class="divider" /> <span>د ډاکټر کاري چاپېریال</span></div>
      <div class="header-actions">
        <UTooltip text="د رنګ حالت بدلول"><UButton :icon="colorMode.value === 'dark' ? 'i-lucide-sun' : 'i-lucide-moon'" color="neutral" variant="ghost" aria-label="د رنګ حالت بدلول" @click="colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'" /></UTooltip>
        <UDropdownMenu :items="menu" :ui="{ content: 'w-64 max-w-[calc(100vw-1.5rem)]', itemLabel: 'whitespace-normal break-words', itemDescription: 'break-all' }"><button type="button" class="profile" aria-label="د حساب غوراوي"><UAvatar :src="doctor.information.value?.doctorPhoto || undefined" :alt="displayName" :text="initials" class="avatar" :ui="{ image: 'size-full object-cover', fallback: 'text-base' }" /><span class="profile-copy"><strong>{{ displayName }}</strong><small dir="auto">{{ username }}</small></span><UIcon name="i-lucide-chevron-down" /></button></UDropdownMenu>
      </div>
    </header>
    <main id="main" class="main-container"><slot /></main>
    <PasswordChangeModal v-model:open="passwordOpen" />
    <BottomNavigation />
  </div>
</template>

<style scoped>
.profile-copy { min-width: 0; max-width: 200px; }
.profile-copy strong, .profile-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.profile .avatar { flex-shrink: 0; }
.main-container { padding-bottom: calc(125px + env(safe-area-inset-bottom, 0px)); }
@media (max-width: 860px) {
  .main-container { padding-bottom: calc(230px + env(safe-area-inset-bottom, 0px)); }
}
@media (max-width: 380px) {
  .main-container { padding-bottom: calc(270px + env(safe-area-inset-bottom, 0px)); }
}
</style>
