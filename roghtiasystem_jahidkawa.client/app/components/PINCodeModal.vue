<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'

const open = defineModel<boolean>('open', { default: false })
const auth = useAuth()
const toast = useToast()
const form = useTemplateRef('pinForm')
const pending = ref(false)
const serviceMessage = ref('')
const credentials = reactive({ currentPassword: '', pinCode: '' })

function validate(state: typeof credentials): FormError[] {
  const errors: FormError[] = []
  if (!state.currentPassword || state.currentPassword.length > 128) errors.push({ name: 'currentPassword', message: 'Enter your current password.' })
  if (!/^[0-9]{6,10}$/.test(state.pinCode)) errors.push({ name: 'pinCode', message: 'PIN must contain 6 to 10 digits.' })
  return errors
}

function focusError(event: FormErrorEvent) {
  if (event.errors[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

async function submit() {
  if (pending.value) return
  pending.value = true
  serviceMessage.value = ''
  try {
    await $fetch('/api/auth/set-pin', {
      method: 'POST', body: { currentPassword: credentials.currentPassword, pinCode: credentials.pinCode },
      headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    Object.assign(credentials, { currentPassword: '', pinCode: '' })
    open.value = false
    toast.add({ title: 'PIN saved', description: 'You can now use this PIN to reset a forgotten password.', color: 'success', icon: 'i-lucide-shield-check' })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    const code = failure.data?.data?.code || failure.data?.code
    const status = failure.statusCode || failure.status
    const messages: Record<string, string> = {
      PASSWORD_INCORRECT: 'Current password is incorrect.',
      VALIDATION_ERROR: 'Check your password and PIN and try again.',
      TOO_MANY_REQUESTS: 'Too many attempts. Please try again later.',
      INVALID_ORIGIN: 'Refresh the page and try again.'
    }
    if (status === 401 && code !== 'PASSWORD_INCORRECT') {
      open.value = false
      await auth.refresh()
      await navigateTo('/login')
      return
    }
    serviceMessage.value = messages[code || ''] || 'PIN could not be saved. Please try again.'
    if (code === 'PASSWORD_INCORRECT') form.value?.setErrors([{ name: 'currentPassword', message: messages.PASSWORD_INCORRECT! }])
  } finally {
    pending.value = false
  }
}

watch(open, value => {
  if (!value) {
    Object.assign(credentials, { currentPassword: '', pinCode: '' })
    serviceMessage.value = ''
    form.value?.clear()
  }
})
watch(credentials, () => { serviceMessage.value = '' })
</script>

<template>
  <UModal :open="open" title="Recovery PIN" description="Enter your current password and choose a PIN with 6 to 10 digits. Keep this PIN in a safe place." :close="pending ? false : { 'aria-label': 'Close' }" :dismissible="!pending" :content="{ dir: 'ltr' }" :ui="{ content: 'max-w-lg min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }" @update:open="value => { if (!pending) open = value }">
    <template #body>
      <UForm ref="pinForm" :state="credentials" :validate="validate" :disabled="pending" novalidate class="pin-form" @submit="submit" @error="focusError">
        <UFormField label="Current password" name="currentPassword" required>
          <UInput v-model="credentials.currentPassword" name="currentPassword" type="password" autocomplete="current-password" placeholder="Enter your current password" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'pin-input' }" />
        </UFormField>
        <UFormField label="PIN" name="pinCode" required help="6 to 10 digits">
          <UInput v-model="credentials.pinCode" name="pinCode" type="text" inputmode="numeric" dir="ltr" autocomplete="off" pattern="[0-9]*" maxlength="10" placeholder="Enter new PIN" icon="i-lucide-key-round" size="xl" class="w-full" :ui="{ base: 'pin-input' }" @update:model-value="value => credentials.pinCode = String(value || '').replace(/[^0-9]/g, '').slice(0, 10)" />
        </UFormField>
        <p v-if="serviceMessage" class="pin-message" role="alert">{{ serviceMessage }}</p>
        <div class="pin-actions">
          <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending" :disabled="pending">Save PIN</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="open = false">Cancel</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<style scoped>
.pin-form { display: flex; flex-direction: column; gap: 20px; min-width: 0; }
.pin-form > * { min-width: 0; }
.pin-form :deep(.pin-input) { min-width: 0; min-height: 46px; font-size: 1rem; }
.pin-message { color: var(--ink); background: var(--soft); padding: 12px 14px; border-radius: 9px; font-size: 1rem; line-height: 1.9; margin: 0; overflow-wrap: anywhere; }
.pin-actions { display: flex; flex-wrap: wrap; gap: 10px; padding-top: 4px; }
.pin-actions :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; font-size: 1rem; white-space: normal; }
@media (max-width: 480px) { .pin-actions > * { flex: 1 1 150px; justify-content: center; } }
</style>
