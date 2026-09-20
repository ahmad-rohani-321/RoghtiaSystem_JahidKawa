<script setup>
const props = defineProps({ mode: { type: String, default: 'login' } })
const isRegister = computed(() => props.mode === 'register')

const credentials = reactive({ username: '', password: '' })
const showPassword = ref(false)
const serviceMessage = ref('')
const rememberMe = ref(false)
const auth = useAuth()
const route = useRoute()
const { pending } = auth

function validate(state) {
  const errors = []
  if (!state.username?.trim()) errors.push({ name: 'username', message: 'خپل کارن نوم ولیکئ.' })
  if (!state.password) errors.push({ name: 'password', message: 'خپل پټنوم ولیکئ.' })
  else if (isRegister.value && state.password.length < 8) errors.push({ name: 'password', message: 'پټنوم باید لږ تر لږه 8 توري ولري.' })
  if (state.username?.trim().length > 64) errors.push({ name: 'username', message: 'کارن نوم باید تر 64 تورو ډېر نه وي.' })
  if (state.password?.length > 128) errors.push({ name: 'password', message: 'پټنوم باید تر 128 تورو ډېر نه وي.' })
  return errors
}

async function submit() {
  serviceMessage.value = ''
  try {
    await auth.authenticate(isRegister.value ? 'register' : 'login', {
      userName: credentials.username.trim(),
      password: credentials.password,
      ...(!isRegister.value ? { rememberMe: rememberMe.value } : {})
    })
    credentials.password = ''
    // Accept only a local application path, never an external redirect.
    const redirect = route.query.redirect
    const destination = typeof redirect === 'string' && /^\/(?!\/)/.test(redirect) && !redirect.includes('\\') && !/^\/(login|register)(?:[/?#]|$)/.test(redirect) ? redirect : '/'
    await navigateTo(destination)
  } catch (error) {
    const code = error?.data?.data?.code || error?.data?.code
    const messages = {
      INVALID_CREDENTIALS: 'کارن نوم یا پټنوم ناسم دی.',
      USERNAME_TAKEN: 'دا کارن نوم مخکې کارول شوی دی. بل نوم وټاکئ.',
      VALIDATION_ERROR: 'کارن نوم او پټنوم وګورئ او بیا هڅه وکړئ.',
      TOO_MANY_REQUESTS: 'ډېرې هڅې شوې دي. لږ وروسته بیا هڅه وکړئ.',
      INVALID_ORIGIN: 'غوښتنه ونه منل شوه. پاڼه تازه کړئ او بیا هڅه وکړئ.'
    }
    serviceMessage.value = messages[code] || 'له سرور سره اړیکه ونه نیول شوه. مهرباني وکړئ بیا هڅه وکړئ.'
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
        <span class="login-brand">روغتیا<span>.</span></span>
        <h1 id="login-title">{{ isRegister ? 'نوی کارن جوړ کړئ' : 'خپل حساب ته ننوځئ' }}</h1>
        <p>{{ isRegister ? 'د نوي حساب لپاره کارن نوم او پټنوم وټاکئ.' : 'د ننوتلو لپاره خپل کارن نوم او پټنوم ولیکئ.' }}</p>
      </div>

      <UForm :state="credentials" :validate="validate" :disabled="pending" novalidate class="login-form" @submit="submit" @error="focusError">
        <UFormField label="کارن نوم" name="username" required>
          <UInput v-model="credentials.username" name="username" autocomplete="username" autocapitalize="none" :spellcheck="false" placeholder="خپل کارن نوم ولیکئ" icon="i-lucide-user-round" size="xl" class="w-full" :ui="{ base: 'login-input' }" />
        </UFormField>

        <UFormField label="پټنوم" name="password" required>
          <UInput v-model="credentials.password" name="password" :type="showPassword ? 'text' : 'password'" :autocomplete="isRegister ? 'new-password' : 'current-password'" placeholder="خپل پټنوم ولیکئ" icon="i-lucide-lock-keyhole" size="xl" class="w-full" :ui="{ base: 'login-input' }">
            <template #trailing>
              <UButton type="button" color="neutral" variant="ghost" size="sm" :icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'" :aria-label="showPassword ? 'پټنوم پټ کړئ' : 'پټنوم ښکاره کړئ'" :aria-pressed="showPassword" @click="showPassword = !showPassword" />
            </template>
          </UInput>
        </UFormField>

        <UCheckbox v-if="!isRegister" v-model="rememberMe" label="ما په یاد وساتئ" name="rememberMe" />
        <p v-if="serviceMessage" class="login-message" role="alert">{{ serviceMessage }}</p>
        <UButton type="submit" size="xl" block :loading="pending" :disabled="pending" trailing-icon="i-lucide-arrow-left" class="login-submit">{{ isRegister ? 'کارن جوړول' : 'ننوتل' }}</UButton>
      </UForm>
      <p class="login-switch"><span>{{ isRegister ? 'حساب لرئ؟' : 'حساب نه لرئ؟' }}</span><NuxtLink :to="isRegister ? '/login' : '/register'">{{ isRegister ? 'خپل حساب ته ننوځئ' : 'نوی کارن جوړ کړئ' }}</NuxtLink></p>
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
@media (max-width: 480px) {
  .login-page { padding: max(24px, env(safe-area-inset-top)) max(16px, env(safe-area-inset-right)) max(24px, env(safe-area-inset-bottom)) max(16px, env(safe-area-inset-left)); }
  .login-card { padding: 28px 20px; }
}
</style>
