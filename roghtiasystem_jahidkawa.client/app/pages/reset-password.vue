<script setup>
definePageMeta({ layout: false })
useHead({ title: 'Reset password — E-Roghtia' })

const verification = reactive({ userName: '', pinCode: '' })
const replacement = reactive({ newPassword: '', confirmPassword: '' })
const step = ref('verify')
const pending = ref(false)
const message = ref('')
const showPassword = ref(false)

function validateVerification(state) {
  const errors = []
  if (!state.userName?.trim() || state.userName.trim().length > 64) errors.push({ name: 'userName', message: 'Enter a valid username.' })
  if (!/^[0-9]{6,10}$/.test(state.pinCode)) errors.push({ name: 'pinCode', message: 'PIN must contain 6 to 10 digits.' })
  return errors
}

function validateReplacement(state) {
  const errors = []
  if (state.newPassword.length < 8 || state.newPassword.length > 128) errors.push({ name: 'newPassword', message: 'New password must contain 8 to 128 characters.' })
  if (state.newPassword === verification.pinCode) errors.push({ name: 'newPassword', message: 'Password must differ from the PIN code.' })
  if (state.confirmPassword !== state.newPassword) errors.push({ name: 'confirmPassword', message: 'Passwords do not match.' })
  return errors
}

function showRequestError(error) {
  const code = error?.data?.data?.code || error?.data?.code || ((error?.statusCode || error?.status) === 401 ? 'INVALID_PIN' : '')
  const messages = {
    INVALID_PIN: 'Username or PIN is incorrect. Please try again.',
    VALIDATION_ERROR: 'Check the information and try again.',
    TOO_MANY_REQUESTS: 'Too many attempts. Please try again later.',
    RESET_CONFLICT: 'Unable to change password. Please try again.',
    INVALID_ORIGIN: 'Refresh the page and try again.'
  }
  if (code === 'INVALID_PIN') {
    step.value = 'verify'
    verification.pinCode = ''
    replacement.newPassword = ''
    replacement.confirmPassword = ''
  }
  message.value = messages[code] || 'Unable to connect to the server. Please try again.'
}

async function verify() {
  if (pending.value) return
  pending.value = true
  message.value = ''
  try {
    await $fetch('/api/auth/verify-pin', {
      method: 'POST', body: { userName: verification.userName.trim(), pinCode: verification.pinCode },
      headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    step.value = 'replace'
  } catch (error) {
    showRequestError(error)
  } finally {
    pending.value = false
  }
}

async function resetPassword() {
  if (pending.value) return
  pending.value = true
  message.value = ''
  try {
    await $fetch('/api/auth/reset-password', {
      method: 'POST', body: { userName: verification.userName.trim(), pinCode: verification.pinCode, newPassword: replacement.newPassword },
      headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    verification.pinCode = ''
    replacement.newPassword = ''
    replacement.confirmPassword = ''
    step.value = 'done'
  } catch (error) {
    showRequestError(error)
  } finally {
    pending.value = false
  }
}

function focusError(event) {
  if (event.errors?.[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

function updatePIN(value) {
  verification.pinCode = String(value || '').replace(/[^0-9]/g, '').slice(0, 10)
  message.value = ''
}
</script>

<template>
  <main class="reset-page" aria-labelledby="reset-title">
    <section class="reset-card">
      <div class="reset-intro">
        <span class="brand-icon reset-logo" aria-hidden="true"><UIcon name="i-lucide-heart-pulse" /></span>
        <span class="reset-brand">E-Roghtia<span>.</span></span>
        <h1 id="reset-title">Reset password</h1>
        <p v-if="step === 'verify'">Enter your username and the PIN you chose when creating your account.</p>
        <p v-else-if="step === 'replace'">PIN verified. Choose a new password.</p>
        <p v-else>Your password has changed. Sign in with your new password.</p>
      </div>

      <UForm v-if="step === 'verify'" :state="verification" :validate="validateVerification" :disabled="pending" novalidate class="reset-form" @submit="verify" @error="focusError">
        <UFormField label="Username" name="userName" required>
          <UInput v-model="verification.userName" name="userName" autocomplete="username" autocapitalize="none" :spellcheck="false" placeholder="Enter your username" icon="i-lucide-user-round" size="xl" class="w-full" :ui="{ base: 'reset-input' }" @update:model-value="message = ''" />
        </UFormField>
        <UFormField label="PIN" name="pinCode" required help="6 to 10 digits">
          <UInput v-model="verification.pinCode" name="pinCode" type="text" inputmode="numeric" dir="ltr" autocomplete="off" pattern="[0-9]*" maxlength="10" placeholder="Enter your PIN" icon="i-lucide-key-round" size="xl" class="w-full" :ui="{ base: 'reset-input' }" @update:model-value="updatePIN" />
        </UFormField>
        <p v-if="message" class="reset-message" role="alert">{{ message }}</p>
        <UButton type="submit" size="xl" block :loading="pending" :disabled="pending" trailing-icon="i-lucide-arrow-right" class="reset-submit">Verify PIN</UButton>
      </UForm>

      <UForm v-else-if="step === 'replace'" :state="replacement" :validate="validateReplacement" :disabled="pending" novalidate class="reset-form" @submit="resetPassword" @error="focusError">
        <UFormField label="New password" name="newPassword" required>
          <UInput v-model="replacement.newPassword" name="newPassword" :type="showPassword ? 'text' : 'password'" autocomplete="new-password" placeholder="Enter new password" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'reset-input' }" @update:model-value="message = ''">
            <template #trailing><UButton type="button" color="neutral" variant="ghost" size="sm" :icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'" :aria-label="showPassword ? 'Hide password' : 'Show password'" :aria-pressed="showPassword" @click="showPassword = !showPassword" /></template>
          </UInput>
        </UFormField>
        <UFormField label="Confirm new password" name="confirmPassword" required>
          <UInput v-model="replacement.confirmPassword" name="confirmPassword" :type="showPassword ? 'text' : 'password'" autocomplete="new-password" placeholder="Confirm new password" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'reset-input' }" @update:model-value="message = ''" />
        </UFormField>
        <p v-if="message" class="reset-message" role="alert">{{ message }}</p>
        <UButton type="submit" size="xl" block :loading="pending" :disabled="pending" trailing-icon="i-lucide-arrow-right" class="reset-submit">Change password</UButton>
        <UButton type="button" color="neutral" variant="ghost" size="lg" block :disabled="pending" @click="step = 'verify'">Back to PIN verification</UButton>
      </UForm>

      <div v-else class="reset-done" role="status"><UIcon name="i-lucide-circle-check" aria-hidden="true" /><p>New password saved. Previous sessions have ended.</p></div>
      <p class="reset-switch"><NuxtLink to="/login">Return to sign in</NuxtLink></p>
    </section>
  </main>
</template>

<style scoped>
.reset-page { min-height: 100vh; min-height: 100dvh; display: grid; grid-template-columns: minmax(0, 1fr); place-items: center; padding: max(32px, env(safe-area-inset-top)) max(20px, env(safe-area-inset-right)) max(32px, env(safe-area-inset-bottom)) max(20px, env(safe-area-inset-left)); background: var(--canvas); }
.reset-card { width: 100%; min-width: 0; max-width: 420px; padding: 38px 34px 34px; background: var(--surface); border: 1px solid var(--line); border-radius: 20px; box-shadow: 0 12px 48px #203b4608; }
.reset-intro { text-align: center; margin-bottom: 30px; }
.reset-logo { width: 52px; height: 52px; margin: 0 auto 12px; }
.reset-brand { display: block; font-size: 27px; font-weight: 800; margin-bottom: 20px; }
.reset-brand > span { color: var(--teal); }
.reset-intro h1 { font-size: 1.5rem; font-weight: 700; margin: 0 0 10px; }
.reset-intro p { color: var(--muted); font-size: 1rem; line-height: 1.9; margin: 0; }
.reset-form { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.reset-form > * { min-width: 0; }
.reset-form :deep(.reset-input) { min-width: 0; min-height: 48px; font-size: 1rem; border-radius: 9px; }
.reset-submit { min-height: 48px; border-radius: 9px; font-size: 1rem; margin-top: 4px; }
.reset-message { color: var(--ink); background: var(--soft); padding: 12px 14px; border-radius: 9px; font-size: 0.875rem; line-height: 2; overflow-wrap: anywhere; }
.reset-switch { text-align: center; margin: 24px 0 0; font-size: 0.875rem; }
.reset-switch a { color: var(--teal); font-weight: 600; text-decoration: underline; text-underline-offset: 4px; }
.reset-done { display: grid; justify-items: center; gap: 10px; padding: 12px; text-align: center; }
.reset-done .iconify { width: 42px; height: 42px; color: var(--teal); }
.reset-done p { margin: 0; line-height: 1.8; }
@media (max-width: 480px) { .reset-page { padding: max(24px, env(safe-area-inset-top)) max(16px, env(safe-area-inset-right)) max(24px, env(safe-area-inset-bottom)) max(16px, env(safe-area-inset-left)); } .reset-card { padding: 28px 20px; } }
</style>
