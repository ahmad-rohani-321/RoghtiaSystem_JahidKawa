<script setup lang="ts">
interface InstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const installPrompt = shallowRef<InstallPromptEvent | null>(null)
const helpOpen = ref(false)
const installing = ref(false)
const installed = ref(false)
const secureOrigin = ref(true)
const installInstructions = ref('Open your browser menu and choose “Install E-Roghtia” or “Add to Home Screen”.')

function captureInstallPrompt(event: Event) {
  event.preventDefault()
  installPrompt.value = event as InstallPromptEvent
}

function onInstalled() {
  installPrompt.value = null
  installed.value = true
}

function showInstallHelp() {
  if (installPrompt.value) {
    void installApp()
    return
  }
  helpOpen.value = true
}

async function installApp() {
  const promptEvent = installPrompt.value
  if (!promptEvent || installing.value) {
    helpOpen.value = true
    return
  }
  installing.value = true
  try {
    await promptEvent.prompt()
    const choice = await promptEvent.userChoice
    if (choice.outcome === 'accepted') onInstalled()
    installPrompt.value = null
  } finally {
    installing.value = false
  }
}

onMounted(() => {
  secureOrigin.value = window.isSecureContext
  const isAppleMobile = /iPhone|iPad|iPod/i.test(navigator.userAgent)
  const isAndroid = /Android/i.test(navigator.userAgent)
  if (!window.isSecureContext) {
    installInstructions.value = 'Installation requires a secure HTTPS address. If you opened E-Roghtia with a network IP address over HTTP, ask your administrator to enable HTTPS first.'
  } else if (isAppleMobile) {
    installInstructions.value = 'In Safari, tap Share, then choose “Add to Home Screen”. The app will open from its new home screen icon.'
  } else if (isAndroid) {
    installInstructions.value = 'In Chrome, open the ⋮ menu and choose “Install app” or “Add to Home screen”.'
  } else {
    installInstructions.value = 'In Chrome or Edge, open the browser menu and choose “Install E-Roghtia” (or “Apps” → “Install this site as an app”). If that option is missing, reload this page once over HTTPS.'
  }
  installed.value = window.matchMedia('(display-mode: standalone)').matches || ('standalone' in navigator && Boolean((navigator as Navigator & { standalone?: boolean }).standalone))
  window.addEventListener('beforeinstallprompt', captureInstallPrompt)
  window.addEventListener('appinstalled', onInstalled)
})
onBeforeUnmount(() => {
  window.removeEventListener('beforeinstallprompt', captureInstallPrompt)
  window.removeEventListener('appinstalled', onInstalled)
})
</script>

<template>
  <UTooltip :text="installed ? 'E-Roghtia is installed' : 'Install E-Roghtia'">
    <UButton
      v-if="!installed"
      type="button"
      color="neutral"
      variant="outline"
      icon="i-lucide-download"
      aria-label="Install E-Roghtia"
      :loading="installing"
      class="pwa-install-button"
      @click="showInstallHelp"
    >
      <span>Install</span>
    </UButton>
    <UButton v-else type="button" color="primary" variant="soft" icon="i-lucide-circle-check" aria-label="E-Roghtia is installed" class="pwa-install-button">
      <span>Installed</span>
    </UButton>
  </UTooltip>

  <UModal v-model:open="helpOpen" title="Install E-Roghtia" description="Add the clinic application to your device for quick access." :content="{ dir: 'ltr' }">
    <template #body>
      <div class="install-help">
        <span class="install-help-icon"><UIcon :name="secureOrigin ? 'i-lucide-smartphone' : 'i-lucide-shield-alert'" /></span>
        <p>{{ installInstructions }}</p>
        <p v-if="secureOrigin" class="install-help-note">If you just opened this site, reload it once to let the browser detect the app.</p>
      </div>
    </template>
    <template #footer>
      <UButton type="button" color="primary" @click="helpOpen = false">Got it</UButton>
    </template>
  </UModal>
</template>

<style scoped>
.pwa-install-button { min-height: 42px; flex-shrink: 0; }
.install-help { display: flex; align-items: flex-start; gap: 14px; }
.install-help-icon { display: grid; place-items: center; width: 42px; height: 42px; flex: 0 0 auto; border-radius: 12px; color: var(--teal); background: var(--soft); }
.install-help p { margin: 0; color: var(--ink); font-size: .95rem; line-height: 1.75; }
.install-help .install-help-note { margin-top: 12px; color: var(--muted); font-size: .85rem; }
@media (max-width: 540px) { .pwa-install-button { min-width: 42px; padding-inline: 9px; }.pwa-install-button span { display: none; } }
</style>
