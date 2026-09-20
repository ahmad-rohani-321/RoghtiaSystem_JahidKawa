<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'

const open = defineModel<boolean>('open', { default: false })
const auth = useAuth()
const toast = useToast()
const form = useTemplateRef('passwordForm')
const pending = ref(false)
const serviceMessage = ref('')
const credentials = reactive({ currentPassword: '', newPassword: '', confirmation: '' })
const visible = reactive({ currentPassword: false, newPassword: false, confirmation: false })
const fields = [
  { name: 'currentPassword', label: 'اوسنی پټنوم', autocomplete: 'current-password' },
  { name: 'newPassword', label: 'نوی پټنوم', autocomplete: 'new-password' },
  { name: 'confirmation', label: 'د نوي پټنوم تایید', autocomplete: 'new-password' }
] as const

function validate(state: typeof credentials): FormError[] {
  const errors: FormError[] = []
  if (!state.currentPassword) errors.push({ name: 'currentPassword', message: 'خپل اوسنی پټنوم ولیکئ.' })
  else if (state.currentPassword.length > 128) errors.push({ name: 'currentPassword', message: 'پټنوم باید تر 128 تورو ډېر نه وي.' })
  if (state.newPassword.length < 8) errors.push({ name: 'newPassword', message: 'نوی پټنوم باید لږ تر لږه 8 توري ولري.' })
  else if (state.newPassword.length > 128) errors.push({ name: 'newPassword', message: 'پټنوم باید تر 128 تورو ډېر نه وي.' })
  else if (state.newPassword === state.currentPassword) errors.push({ name: 'newPassword', message: 'نوی پټنوم باید له اوسني پټنوم څخه توپیر ولري.' })
  if (!state.confirmation) errors.push({ name: 'confirmation', message: 'نوی پټنوم بیا ولیکئ.' })
  else if (state.confirmation !== state.newPassword) errors.push({ name: 'confirmation', message: 'دواړه نوي پټنومونه باید یو شان وي.' })
  return errors
}

function reset() {
  Object.assign(credentials, { currentPassword: '', newPassword: '', confirmation: '' })
  Object.assign(visible, { currentPassword: false, newPassword: false, confirmation: false })
  serviceMessage.value = ''
  form.value?.clear()
}

function focusError(event: FormErrorEvent) {
  if (event.errors[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

async function returnToLogin() {
  open.value = false
  await auth.refresh()
  await navigateTo('/login')
}

async function submit() {
  if (pending.value) return
  pending.value = true
  serviceMessage.value = ''
  let saved = false
  let focusCurrentPassword = false
  try {
    await $fetch('/api/auth/change-password', {
      method: 'POST',
      body: { currentPassword: credentials.currentPassword, newPassword: credentials.newPassword },
      headers: { 'x-roghtia-request': '1' },
      retry: 0
    })
    saved = true
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    const code = failure.data?.data?.code || failure.data?.code
    const status = failure.statusCode || failure.status
    const messages: Record<string, string> = {
      PASSWORD_INCORRECT: 'اوسنی پټنوم ناسم دی.',
      VALIDATION_ERROR: 'پټنومونه وګورئ او بیا هڅه وکړئ.',
      TOO_MANY_REQUESTS: 'ډېرې هڅې شوې دي. لږ وروسته بیا هڅه وکړئ.',
      INVALID_ORIGIN: 'غوښتنه ونه منل شوه. پاڼه تازه کړئ او بیا هڅه وکړئ.'
    }
    if (code === 'PASSWORD_INCORRECT') {
      form.value?.setErrors([{ name: 'currentPassword', message: messages.PASSWORD_INCORRECT! }])
      focusCurrentPassword = true
    }
    if (status === 401 && code !== 'PASSWORD_INCORRECT') {
      toast.add({ title: 'بیا ننوځئ', description: 'ستاسې ناسته پای ته رسېدلې ده. بیا خپل حساب ته ننوځئ.', color: 'warning' })
      await returnToLogin()
      return
    }
    serviceMessage.value = messages[code || ''] || (status === 429 ? messages.TOO_MANY_REQUESTS! : 'پټنوم بدل نه شو. مهرباني وکړئ بیا هڅه وکړئ.')
  } finally {
    pending.value = false
  }
  if (focusCurrentPassword) {
    await nextTick()
    const input = form.value?.getErrors('currentPassword')[0]?.id
    if (input) document.getElementById(input)?.focus()
  }
  if (saved) {
    toast.add({ title: 'پټنوم بدل شو', description: 'له نوي پټنوم سره بیا خپل حساب ته ننوځئ.', color: 'success', icon: 'i-lucide-shield-check' })
    await returnToLogin()
  }
}

watch(open, (value) => { if (!value) reset() })
watch(credentials, () => { serviceMessage.value = '' })
</script>

<template>
  <UModal
    :open="open"
    title="د پټنوم بدلول"
    description="د پټنوم له بدلولو وروسته له نوي پټنوم سره بیا ننوځئ."
    :close="pending ? false : { 'aria-label': 'تړل' }"
    :dismissible="!pending"
    :content="{ dir: 'rtl' }"
    :ui="{ content: 'max-w-lg min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }"
    @update:open="value => { if (!pending) open = value }"
  >
    <template #body>
      <UForm ref="passwordForm" :state="credentials" :validate="validate" :disabled="pending" :loading-auto="false" novalidate class="password-form" @submit="submit" @error="focusError">
        <UFormField v-for="field in fields" :key="field.name" :label="field.label" :name="field.name" size="lg" required>
          <UInput v-model="credentials[field.name]" :name="field.name" :type="visible[field.name] ? 'text' : 'password'" :autocomplete="field.autocomplete" :placeholder="field.label" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'password-input' }">
            <template #trailing>
              <UButton type="button" color="neutral" variant="ghost" size="sm" :disabled="pending" :icon="visible[field.name] ? 'i-lucide-eye-off' : 'i-lucide-eye'" :aria-label="`${field.label}: ${visible[field.name] ? 'پټ کړئ' : 'ښکاره کړئ'}`" :aria-pressed="visible[field.name]" @click="visible[field.name] = !visible[field.name]" />
            </template>
          </UInput>
        </UFormField>
        <p class="password-help">نوی پټنوم باید له 8 تر 128 تورو پورې وي.</p>
        <p v-if="serviceMessage" class="password-message" role="alert">{{ serviceMessage }}</p>
        <div class="password-actions">
          <UButton type="submit" size="lg" icon="i-lucide-key-round" :loading="pending" :disabled="pending">پټنوم بدل کړئ</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="open = false">بندول</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<style scoped>
.password-form { display: flex; flex-direction: column; gap: 20px; min-width: 0; }
.password-form > * { min-width: 0; }
.password-form :deep(.password-input) { min-width: 0; min-height: 46px; font-size: 1rem; }
.password-help { color: var(--muted); font-size: 0.875rem; line-height: 1.8; margin: 0; }
.password-message { color: var(--ink); background: var(--soft); padding: 12px 14px; border-radius: 9px; font-size: 1rem; line-height: 1.9; margin: 0; overflow-wrap: anywhere; }
.password-actions { display: flex; flex-wrap: wrap; gap: 10px; padding-top: 4px; }
.password-actions :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; font-size: 1rem; white-space: normal; }
@media (max-width: 480px) { .password-actions > * { flex: 1 1 150px; justify-content: center; } }
</style>
