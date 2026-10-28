<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'
import type { DoctorImageField, DoctorInformation } from '#shared/types/doctor-information'

useHead({ title: 'Settings — E-Roghtia' })

const profile = useDoctorInformation()
const auth = useAuth()
const toast = useToast()
const doctorForm = useTemplateRef('doctorForm')
const loading = ref(true)
const loaded = ref(false)
const pending = ref(false)
const resetOpen = ref(false)
const pinOpen = ref(false)
const serviceMessage = ref('')
const revision = ref(0)
const fields = [
  { name: 'doctorNamePashto', label: 'Doctor name (Pashto)', max: 160, direction: 'auto' },
  { name: 'doctorNameEnglish', label: 'Doctor name (English)', max: 160, direction: 'ltr' },
  { name: 'doctorProfessionPashto', label: 'Doctor profession (Pashto)', max: 200, direction: 'auto', multiline: true },
  { name: 'doctorProfessionEnglish', label: 'Doctor profession (English)', max: 200, direction: 'ltr', multiline: true },
  { name: 'hospitalNamePashto', label: 'Hospital name (Pashto)', max: 200, direction: 'auto' },
  { name: 'hospitalAddress', label: 'Hospital address', max: 300, direction: 'auto' },
  { name: 'hospitalPhoneNumbers', label: 'Hospital phone number(s)', max: 300, direction: 'ltr' }
] as const
const imageFields: { name: DoctorImageField, label: string, portrait?: boolean }[] = [
  { name: 'doctorPhoto', label: 'Doctor photo', portrait: true },
  { name: 'doctorLogo', label: 'Doctor logo' },
  { name: 'hospitalLogo', label: 'Hospital logo' }
]
const form = reactive({ doctorNameEnglish: '', doctorNamePashto: '', doctorProfessionPashto: '', doctorProfessionEnglish: '', hospitalNamePashto: '', hospitalAddress: '', hospitalPhoneNumbers: '' })
const baseline = ref(JSON.stringify(form))
const images = reactive<Record<DoctorImageField, { file: File | null, removed: boolean }>>({
  doctorPhoto: { file: null, removed: false },
  doctorLogo: { file: null, removed: false },
  hospitalLogo: { file: null, removed: false }
})
const dirty = computed(() => JSON.stringify(form) !== baseline.value || imageFields.some(field => !!images[field.name].file || images[field.name].removed))
const busy = computed(() => loading.value || pending.value)
const canReset = computed(() => loaded.value && !busy.value && (profile.information.value?.exists || dirty.value))

function populate(value: DoctorInformation | null) {
  for (const field of fields) form[field.name] = value?.[field.name] || ''
  for (const field of imageFields) Object.assign(images[field.name], { file: null, removed: false })
  baseline.value = JSON.stringify(form)
  revision.value++
  doctorForm.value?.clear()
}

function validate(state: typeof form): FormError[] {
  const errors: FormError[] = []
  for (const field of fields) {
    if (state[field.name].trim().length > field.max) errors.push({ name: field.name, message: `This field must not exceed ${field.max === 160 ? '160' : '200'} characters.` })
  }
  for (const field of imageFields) {
    const file = images[field.name].file
    if (!file) continue
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) errors.push({ name: field.name, message: 'Only PNG, JPEG and WebP images are accepted.' })
    if (file.size === 0 || file.size > 2 * 1024 * 1024) errors.push({ name: field.name, message: 'Image must not be empty or larger than 2 MB.' })
  }
  return errors
}

function focusError(event: FormErrorEvent) {
  if (event.errors[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

async function showError(error: unknown, title: string, notify = true) {
  const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
  const code = failure.data?.data?.code || failure.data?.code
  const status = failure.statusCode || failure.status
  const messages: Record<string, string> = {
    INVALID_IMAGE: 'Invalid image. Select a valid PNG, JPEG or WebP image.',
    IMAGE_TOO_LARGE: 'Each image must not exceed 2 MB.',
    VALIDATION_ERROR: 'Check the information and images, then try again.',
    DOCTOR_INFORMATION_EXISTS: 'Your information has already been saved. Refresh the page and try again.',
    NOT_FOUND: 'Information not found. Refresh the page and try again.',
    TOO_MANY_REQUESTS: 'Too many attempts. Please try again later.',
    INVALID_ORIGIN: 'Request rejected. Refresh the page and try again.'
  }
  if (status === 401) {
    toast.add({ title: 'Sign in again', description: 'Your session has expired. Please sign in again.', color: 'warning' })
    resetOpen.value = false
    await auth.refresh()
    await navigateTo('/login')
    return
  }
  serviceMessage.value = messages[code || ''] || (status === 429 ? messages.TOO_MANY_REQUESTS! : 'Unable to connect to the server. Please try again.')
  if (notify) toast.add({ title, description: serviceMessage.value, color: 'error' })
}

async function loadInformation() {
  if (pending.value) return
  loading.value = true
  loaded.value = false
  serviceMessage.value = ''
  try {
    populate(await profile.load(true))
    loaded.value = true
  } catch (error) {
    await showError(error, 'Unable to load information', false)
  } finally {
    loading.value = false
  }
}

async function save() {
  if (busy.value || !loaded.value || !dirty.value) return
  pending.value = true
  serviceMessage.value = ''
  try {
    const body = new FormData()
    for (const field of fields) body.set(field.name, form[field.name].trim())
    for (const field of imageFields) {
      const image = images[field.name]
      if (image.file) body.set(field.name, image.file)
      const removeField = `remove${field.name[0]!.toUpperCase()}${field.name.slice(1)}`
      body.set(removeField, String(image.removed))
    }
    populate(await profile.save(body))
    toast.add({ title: 'Information saved', description: 'Your doctor information has been updated.', color: 'success', icon: 'i-lucide-circle-check' })
  } catch (error) {
    await showError(error, 'Information could not be saved')
  } finally {
    pending.value = false
  }
}

async function reset() {
  if (!canReset.value) return
  pending.value = true
  serviceMessage.value = ''
  try {
    populate(await profile.reset())
    resetOpen.value = false
    toast.add({ title: 'Defaults restored', description: 'Your doctor information and images have been cleared.', color: 'success' })
  } catch (error) {
    await showError(error, 'Unable to restore defaults')
  } finally {
    pending.value = false
  }
}

function confirmReset() {
  serviceMessage.value = ''
  resetOpen.value = true
}

if (profile.information.value) {
  populate(profile.information.value)
  loaded.value = true
  loading.value = false
}

onMounted(loadInformation)
</script>

<template>
  <section class="settings-page" aria-labelledby="settings-title">
    <div class="page-heading">
      <div>
        <div class="eyebrow"><span /> Your account, your information</div>
        <h1 id="settings-title">Settings</h1>
        <p>Manage doctor and hospital information here.</p>
      </div>
    </div>

    <section class="panel doctor-settings" aria-labelledby="doctor-information-title" :aria-busy="busy">
      <header class="settings-section-heading">
        <span class="settings-section-icon" aria-hidden="true"><UIcon name="i-lucide-stethoscope" /></span>
        <div><h2 id="doctor-information-title">Doctor information</h2><p>Save names, profession and images for your account.</p></div>
      </header>

      <div v-if="loading" class="settings-state" role="status">
        <UIcon name="i-lucide-loader-circle" class="animate-spin" aria-hidden="true" />
        <p>Loading information…</p>
      </div>
      <div v-else-if="!loaded" class="settings-state">
        <UIcon name="i-lucide-cloud-off" aria-hidden="true" />
        <p role="alert">{{ serviceMessage || 'Unable to load information.' }}</p>
        <UButton type="button" color="neutral" variant="outline" size="lg" icon="i-lucide-refresh-cw" @click="loadInformation">Try again</UButton>
      </div>
      <UForm v-else ref="doctorForm" :state="form" :validate="validate" :disabled="busy" novalidate class="doctor-form" @submit="save" @error="focusError">
        <div class="settings-text-grid">
          <UFormField v-for="field in fields" :key="field.name" :label="field.label" :name="field.name" size="lg" :class="{ 'full-width': field.name.startsWith('hospital') || field.multiline }" :ui="{ label: 'text-base' }">
            <UTextarea v-if="field.multiline" v-model="form[field.name]" :name="field.name" :dir="field.direction" :maxlength="field.max" :placeholder="field.label" :spellcheck="false" :rows="3" class="w-full" :ui="{ base: 'settings-input' }" />
            <UInput v-else v-model="form[field.name]" :name="field.name" :dir="field.direction" :maxlength="field.max" :placeholder="field.name === 'hospitalPhoneNumbers' ? 'Separate multiple phone numbers with commas' : field.label" :spellcheck="false" autocomplete="off" size="xl" class="w-full" :ui="{ base: 'settings-input' }" />
          </UFormField>
        </div>

        <div class="settings-images-section">
          <h3>Photos and logos</h3>
          <p class="settings-section-note">Image changes are applied when you save.</p>
          <div class="settings-images-grid">
            <SettingsImageInput v-for="field in imageFields" :key="`${revision}-${field.name}`" v-model="images[field.name].file" v-model:removed="images[field.name].removed" :name="field.name" :label="field.label" :current="profile.information.value?.[field.name]" :portrait="field.portrait" :disabled="busy" />
          </div>
        </div>

        <p v-if="serviceMessage" class="settings-message" role="alert">{{ serviceMessage }}</p>
        <footer class="settings-actions">
          <span class="settings-save-state" aria-live="polite"><UIcon :name="dirty ? 'i-lucide-pencil-line' : 'i-lucide-circle-check'" aria-hidden="true" />{{ dirty ? 'You have unsaved changes' : 'All information is up to date' }}</span>
          <div class="settings-buttons">
            <UButton type="button" color="neutral" variant="outline" size="lg" icon="i-lucide-rotate-ccw" :disabled="!canReset" @click="confirmReset">Reset</UButton>
            <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending && !resetOpen" :disabled="busy || !dirty" >Save changes</UButton>
          </div>
        </footer>
      </UForm>
    </section>

    <UModal :open="resetOpen" title="Restore defaults" description="Your saved doctor information and all three images will be cleared. This action cannot be undone." :dismissible="!pending" :close="pending ? false : { 'aria-label': 'Close' }" :content="{ dir: 'ltr' }" :ui="{ content: 'min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-8 break-words' }" @update:open="value => { if (!pending) resetOpen = value }">
      <template #body><p>Reset all information to defaults?</p><p v-if="serviceMessage" class="settings-message" role="alert">{{ serviceMessage }}</p></template>
      <template #footer>
        <div class="settings-reset-actions">
          <UButton type="button" size="lg" icon="i-lucide-rotate-ccw" :loading="pending" :disabled="pending" @click="reset">Yes, reset</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="resetOpen = false">Cancel</UButton>
        </div>
      </template>
    </UModal>
    <section class="panel pin-settings" aria-labelledby="pin-settings-title">
      <header class="settings-section-heading">
        <span class="settings-section-icon" aria-hidden="true"><UIcon name="i-lucide-key-round" /></span>
        <div><h2 id="pin-settings-title">Password recovery PIN</h2><p>Choose a recovery PIN or update it. Existing accounts can also set a PIN here.</p></div>
      </header>
      <div class="pin-settings-action"><UButton type="button" size="lg" icon="i-lucide-shield-check" @click="pinOpen = true">Set or change PIN</UButton></div>
    </section>
    <PINCodeModal v-model:open="pinOpen" />
  </section>
</template>

<style scoped>
.settings-page { width: 100%; min-width: 0; max-width: 1100px; margin-inline: auto; }
.settings-page .page-heading p { font-size: 1rem; }
.doctor-settings { min-width: 0; border-radius: 16px; }
.pin-settings { min-width: 0; margin-top: 22px; border-radius: 16px; }
.pin-settings-action { padding: 22px 30px; }
.settings-section-heading { display: flex; align-items: flex-start; gap: 15px; padding: 26px 30px; border-bottom: 1px solid var(--line); }
.settings-section-heading > div { min-width: 0; }
.settings-section-icon { display: grid; place-items: center; flex-shrink: 0; width: 46px; height: 46px; border-radius: 13px; color: var(--teal); background: var(--soft); }
.settings-section-icon .iconify { width: 25px; height: 25px; }
.settings-section-heading h2 { margin: 0 0 5px; font-size: 1.25rem; font-weight: 650; }
.settings-section-heading p { margin: 0; font-size: 1rem; color: var(--muted); line-height: 1.9; }
.doctor-form { min-width: 0; padding: 28px 30px 0; }
.settings-text-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 23px 24px; }
.settings-text-grid > * { min-width: 0; }
.full-width { grid-column: 1 / -1; }
.doctor-form :deep(.settings-input) { min-width: 0; min-height: 48px; font-size: 1rem; border-radius: 9px; }
.settings-images-section { margin-top: 32px; padding-top: 25px; border-top: 1px solid var(--line); }
.settings-images-section h3 { margin: 0 0 7px; font-size: 1.125rem; font-weight: 650; }
.settings-section-note { margin: 0 0 20px; color: var(--muted); font-size: 1rem; line-height: 1.9; }
.settings-images-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 18px; }
.settings-message { color: var(--ink); background: var(--soft); padding: 14px 16px; border-radius: 9px; margin: 22px 0 0; font-size: 1rem; line-height: 1.9; overflow-wrap: anywhere; }
.settings-actions { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 18px; margin: 28px -30px 0; padding: 22px 30px; border-top: 1px solid var(--line); border-radius: 0 0 16px 16px; background: var(--canvas); }
.settings-save-state { min-width: 0; display: flex; align-items: center; gap: 8px; font-size: 0.875rem; color: var(--muted); }
.settings-save-state .iconify { flex-shrink: 0; color: var(--teal); }
.settings-buttons, .settings-reset-actions { min-width: 0; display: flex; flex-wrap: wrap; gap: 10px; }
.settings-buttons :deep(button), .settings-reset-actions :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; font-size: 1rem; white-space: normal; }
.settings-reset-actions { width: 100%; }
.settings-state { min-height: 300px; display: flex; flex-direction: column; justify-content: center; align-items: center; gap: 18px; padding: 36px 24px; text-align: center; color: var(--muted); }
.settings-state > .iconify { width: 30px; height: 30px; color: var(--teal); }
.settings-state p { max-width: 520px; margin: 0; font-size: 1rem; line-height: 1.9; }
@media (max-width: 700px) {
  .pin-settings-action { padding: 20px; }
  .settings-section-heading { padding: 22px 20px; }
  .doctor-form { padding: 23px 20px 0; }
  .settings-text-grid, .settings-images-grid { grid-template-columns: minmax(0, 1fr); }
  .settings-actions { margin-inline: -20px; padding: 20px; }
  .settings-buttons { width: 100%; }
  .settings-buttons > * { flex: 1 1 170px; justify-content: center; }
}
@media (max-width: 480px) {
  .pin-settings-action { padding: 18px 16px; }
  .settings-section-heading { padding: 20px 16px; gap: 12px; }
  .doctor-form { padding: 22px 16px 0; }
  .settings-actions { margin-inline: -16px; padding: 20px 16px; }
  .settings-reset-actions > * { flex: 1 1 160px; justify-content: center; }
}
</style>
