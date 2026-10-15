<script setup>
const props = defineProps({ mode: { type: String, default: 'login' } })
const isRegister = computed(() => props.mode === 'register')

const credentials = reactive({ username: '', password: '', pinCode: '' })
const showPassword = ref(false)
const serviceMessage = ref('')
const rememberMe = ref(false)
const auth = useAuth()
const route = useRoute()
const { pending } = auth

function validate(state) {
  const errors = []
  if (!state.username?.trim()) errors.push({ name: 'username', message: 'Enter your username.' })
  if (!state.password) errors.push({ name: 'password', message: 'Enter your password.' })
  else if (isRegister.value && state.password.length < 8) errors.push({ name: 'password', message: 'Password must contain at least 8 characters.' })
  if (state.username?.trim().length > 64) errors.push({ name: 'username', message: 'Username must not exceed 64 characters.' })
  if (state.password?.length > 128) errors.push({ name: 'password', message: 'Password must not exceed 128 characters.' })
  if (isRegister.value && !/^[0-9]{6,10}$/.test(state.pinCode)) errors.push({ name: 'pinCode', message: 'PIN must contain 6 to 10 digits.' })
  return errors
}

async function submit() {
  serviceMessage.value = ''
  try {
    await auth.authenticate(isRegister.value ? 'register' : 'login', {
      userName: credentials.username.trim(),
      password: credentials.password,
      ...(isRegister.value ? { pinCode: credentials.pinCode } : {}),
      ...(!isRegister.value ? { rememberMe: rememberMe.value } : {})
    })
    credentials.password = ''
    credentials.pinCode = ''
    // Accept only a local application path, never an external redirect.
    const redirect = route.query.redirect
    const destination = typeof redirect === 'string' && /^\/(?!\/)/.test(redirect) && !redirect.includes('\\') && !/^\/(login|register)(?:[/?#]|$)/.test(redirect) ? redirect : '/'
    await navigateTo(destination)
  } catch (error) {
    const code = error?.data?.data?.code || error?.data?.code
    const messages = {
      INVALID_CREDENTIALS: 'Incorrect username or password.',
      USERNAME_TAKEN: 'This username is already taken. Choose another.',
      VALIDATION_ERROR: 'Check your username, password and PIN and try again.',
      TOO_MANY_REQUESTS: 'Too many attempts. Please try again later.',
      INVALID_ORIGIN: 'Request rejected. Refresh the page and try again.'
    }
    serviceMessage.value = messages[code] || 'Unable to connect to the server. Please try again.'
  }
}

function focusError(event) {
  if (event.errors?.[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

watch(credentials, () => { serviceMessage.value = '' })
</script>

<template>
  <main class="login-page" aria-labelledby="login-title">
    <section class="login-card">
      <div class="login-intro">
        <span class="brand-icon login-logo" aria-hidden="true"><UIcon name="i-lucide-heart-pulse" /></span>
        <span class="login-brand">Roghtia<span>.</span></span>
        <h1 id="login-title">{{ isRegister ? 'Create an account' : 'Sign in to your account' }}</h1>
        <p>{{ isRegister ? 'Choose a username, password and recovery PIN for your new account.' : 'Enter your username and password to sign in.' }}</p>
      </div>

      <UForm :state="credentials" :validate="validate" :disabled="pending" novalidate class="login-form" @submit="submit" @error="focusError">
        <UFormField label="Username" name="username" required>
          <UInput v-model="credentials.username" name="username" autocomplete="username" autocapitalize="none" :spellcheck="false" placeholder="Enter your username" icon="i-lucide-user-round" size="xl" class="w-full" :ui="{ base: 'login-input' }" />
        </UFormField>

        <UFormField label="Password" name="password" required>
          <UInput v-model="credentials.password" name="password" :type="showPassword ? 'text' : 'password'" :autocomplete="isRegister ? 'new-password' : 'current-password'" placeholder="Enter your password" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'login-input' }">
            <template #trailing>
              <UButton type="button" color="neutral" variant="ghost" size="sm" :icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'" :aria-label="showPassword ? 'Hide password' : 'Show password'" :aria-pressed="showPassword" @click="showPassword = !showPassword" />
            </template>
          </UInput>
        </UFormField>

        <UFormField v-if="isRegister" label="Password recovery PIN" name="pinCode" required help="6 to 10 digits. Keep this PIN in a safe place.">
          <UInput v-model="credentials.pinCode" name="pinCode" type="text" inputmode="numeric" dir="ltr" autocomplete="off" pattern="[0-9]*" maxlength="10" placeholder="6–10 digits" icon="i-lucide-key-round" size="xl" class="w-full" :ui="{ base: 'login-input' }" @update:model-value="value => credentials.pinCode = String(value || '').replace(/[^0-9]/g, '').slice(0, 10)" />
        </UFormField>

        <UCheckbox v-if="!isRegister" v-model="rememberMe" label="Remember me" name="rememberMe" />
        <NuxtLink v-if="!isRegister" to="/reset-password" class="forgot-link">Forgot password?</NuxtLink>
        <p v-if="serviceMessage" class="login-message" role="alert">{{ serviceMessage }}</p>
        <UButton type="submit" size="xl" block :loading="pending" :disabled="pending" trailing-icon="i-lucide-arrow-right" class="login-submit">{{ isRegister ? 'Create account' : 'Sign in' }}</UButton>
      </UForm>
      <p class="login-switch"><span>{{ isRegister ? 'Already have an account?' : 'Need an account?' }}</span><NuxtLink :to="isRegister ? '/login' : '/register'">{{ isRegister ? 'Sign in to your account' : 'Create an account' }}</NuxtLink></p>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  min-height: 100dvh;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  place-items: center;
  padding: max(32px, env(safe-area-inset-top)) max(20px, env(safe-area-inset-right)) max(32px, env(safe-area-inset-bottom)) max(20px, env(safe-area-inset-left));
  background: var(--canvas);
}
.login-card {
  width: 100%;
  min-width: 0;
  max-width: 420px;
  padding: 38px 34px 34px;
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: 20px;
  box-shadow: 0 12px 48px #203b4608;
}
.login-intro { text-align: center; margin-bottom: 30px; }
.login-logo { width: 52px; height: 52px; margin: 0 auto 12px; }
.login-brand { display: block; font-size: 27px; font-weight: 800; margin-bottom: 20px; }
.login-brand > span { color: var(--teal); }
.login-intro h1 { font-size: 1.5rem; font-weight: 700; margin: 0 0 10px; }
.login-intro p { color: var(--muted); font-size: 1rem; line-height: 1.9; margin: 0; }
.login-form { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.login-form > * { min-width: 0; }
.login-form :deep(.login-input) { min-width: 0; min-height: 48px; font-size: 1rem; border-radius: 9px; }
.login-submit { min-height: 48px; border-radius: 9px; font-size: 1rem; margin-top: 4px; }
.login-message { color: var(--ink); background: var(--soft); padding: 12px 14px; border-radius: 9px; font-size: 0.875rem; line-height: 2; overflow-wrap: anywhere; }
.login-switch { display: flex; flex-wrap: wrap; justify-content: center; gap: 6px; margin: 24px 0 0; color: var(--muted); font-size: 0.875rem; }
.login-switch a { color: var(--teal); font-weight: 600; text-decoration: underline; text-underline-offset: 4px; }
.forgot-link { align-self: flex-start; color: var(--teal); font-size: 0.9375rem; font-weight: 600; text-decoration: underline; text-underline-offset: 4px; }
.forgot-link:focus-visible { outline: 2px solid var(--teal); outline-offset: 4px; border-radius: 3px; }
@media (max-width: 480px) {
  .login-page { padding: max(24px, env(safe-area-inset-top)) max(16px, env(safe-area-inset-right)) max(24px, env(safe-area-inset-bottom)) max(16px, env(safe-area-inset-left)); }
  .login-card { padding: 28px 20px; }
}
</style>
