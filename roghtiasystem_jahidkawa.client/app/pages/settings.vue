<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'
import type { DoctorImageField, DoctorInformation } from '#shared/types/doctor-information'

useHead({ title: 'تنظیمات — روغتیا' })

const profile = useDoctorInformation()
const auth = useAuth()
const toast = useToast()
const doctorForm = useTemplateRef('doctorForm')
const loading = ref(true)
const loaded = ref(false)
const pending = ref(false)
const resetOpen = ref(false)
const serviceMessage = ref('')
const revision = ref(0)
const fields = [
  { name: 'doctorNamePashto', label: 'د ډاکټر نوم په پښتو', max: 160, direction: 'rtl' },
  { name: 'doctorNameEnglish', label: 'د ډاکټر نوم په انګلیسي', max: 160, direction: 'ltr' },
  { name: 'doctorProfessionPashto', label: 'د ډاکټر مسلک په پښتو', max: 200, direction: 'rtl' },
  { name: 'doctorProfessionEnglish', label: 'د ډاکټر مسلک په انګلیسي', max: 200, direction: 'ltr' },
  { name: 'hospitalNamePashto', label: 'د روغتون نوم په پښتو', max: 200, direction: 'rtl' }
] as const
const imageFields: { name: DoctorImageField, label: string, portrait?: boolean }[] = [
  { name: 'doctorPhoto', label: 'د ډاکټر انځور', portrait: true },
  { name: 'doctorLogo', label: 'د ډاکټر لوګو' },
  { name: 'hospitalLogo', label: 'د روغتون لوګو' }
]
const form = reactive({ doctorNameEnglish: '', doctorNamePashto: '', doctorProfessionPashto: '', doctorProfessionEnglish: '', hospitalNamePashto: '' })
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
    if (state[field.name].trim().length > field.max) errors.push({ name: field.name, message: `دا برخه باید تر ${field.max === 160 ? '160' : '200'} تورو ډېره نه وي.` })
  }
  for (const field of imageFields) {
    const file = images[field.name].file
    if (!file) continue
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) errors.push({ name: field.name, message: 'یوازې PNG، JPEG او WebP انځورونه منل کېږي.' })
    if (file.size === 0 || file.size > 2 * 1024 * 1024) errors.push({ name: field.name, message: 'انځور باید تش نه وي او له 2 مېګابایټه لوی نه وي.' })
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
    INVALID_IMAGE: 'انځور ناسم دی. یو سم PNG، JPEG یا WebP انځور وټاکئ.',
    IMAGE_TOO_LARGE: 'د هر انځور اندازه باید له 2 مېګابایټه زیاته نه وي.',
    VALIDATION_ERROR: 'معلومات او انځورونه وګورئ او بیا هڅه وکړئ.',
    DOCTOR_INFORMATION_EXISTS: 'ستاسې معلومات مخکې خوندي شوي دي. پاڼه تازه کړئ او بیا هڅه وکړئ.',
    NOT_FOUND: 'معلومات ونه موندل شول. پاڼه تازه کړئ او بیا هڅه وکړئ.',
    TOO_MANY_REQUESTS: 'ډېرې هڅې شوې دي. لږ وروسته بیا هڅه وکړئ.',
    INVALID_ORIGIN: 'غوښتنه ونه منل شوه. پاڼه تازه کړئ او بیا هڅه وکړئ.'
  }
  if (status === 401) {
    toast.add({ title: 'بیا ننوځئ', description: 'ستاسې ناسته پای ته رسېدلې ده. بیا خپل حساب ته ننوځئ.', color: 'warning' })
    resetOpen.value = false
    await auth.refresh()
    await navigateTo('/login')
    return
  }
  serviceMessage.value = messages[code || ''] || (status === 429 ? messages.TOO_MANY_REQUESTS! : 'له سرور سره اړیکه ونه نیول شوه. مهرباني وکړئ بیا هڅه وکړئ.')
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
    await showError(error, 'معلومات را نه وړل شول', false)
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
    toast.add({ title: 'معلومات خوندي شول', description: 'ستاسې د ډاکټر معلومات تازه شول.', color: 'success', icon: 'i-lucide-circle-check' })
  } catch (error) {
    await showError(error, 'معلومات خوندي نه شول')
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
    toast.add({ title: 'لومړني حالت ته راوګرځېدل', description: 'ستاسې د ډاکټر معلومات او انځورونه پاک شول.', color: 'success' })
  } catch (error) {
    await showError(error, 'معلومات بېرته را ونه ګرځېدل')
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
        <div class="eyebrow"><span /> ستاسې حساب، ستاسې معلومات</div>
        <h1 id="settings-title">تنظیمات</h1>
        <p>د ډاکټر او روغتون معلومات دلته سمبال کړئ.</p>
      </div>
    </div>

    <section class="panel doctor-settings" aria-labelledby="doctor-information-title" :aria-busy="busy">
      <header class="settings-section-heading">
        <span class="settings-section-icon" aria-hidden="true"><UIcon name="i-lucide-stethoscope" /></span>
        <div><h2 id="doctor-information-title">د ډاکټر اړوند معلومات</h2><p>نومونه، مسلک او انځورونه د خپل حساب لپاره خوندي کړئ.</p></div>
      </header>

      <div v-if="loading" class="settings-state" role="status">
        <UIcon name="i-lucide-loader-circle" class="animate-spin" aria-hidden="true" />
        <p>معلومات را اخیستل کېږي…</p>
      </div>
      <div v-else-if="!loaded" class="settings-state">
        <UIcon name="i-lucide-cloud-off" aria-hidden="true" />
        <p role="alert">{{ serviceMessage || 'معلومات را نه وړل شول.' }}</p>
        <UButton type="button" color="neutral" variant="outline" size="lg" icon="i-lucide-refresh-cw" @click="loadInformation">بیا هڅه وکړئ</UButton>
      </div>
      <UForm v-else ref="doctorForm" :state="form" :validate="validate" :disabled="busy" novalidate class="doctor-form" @submit="save" @error="focusError">
        <div class="settings-text-grid">
          <UFormField v-for="field in fields" :key="field.name" :label="field.label" :name="field.name" size="lg" :class="{ 'full-width': field.name === 'hospitalNamePashto' }" :ui="{ label: 'text-base' }">
            <UInput v-model="form[field.name]" :name="field.name" :dir="field.direction" :maxlength="field.max" :placeholder="field.label" :spellcheck="false" autocomplete="off" size="xl" class="w-full" :ui="{ base: 'settings-input' }" />
          </UFormField>
        </div>

        <div class="settings-images-section">
          <h3>انځورونه او لوګوګانې</h3>
          <p class="settings-section-note">د انځور بدلونونه د خوندي کولو له تڼۍ وروسته ثبتېږي.</p>
          <div class="settings-images-grid">
            <SettingsImageInput v-for="field in imageFields" :key="`${revision}-${field.name}`" v-model="images[field.name].file" v-model:removed="images[field.name].removed" :name="field.name" :label="field.label" :current="profile.information.value?.[field.name]" :portrait="field.portrait" :disabled="busy" />
          </div>
        </div>

        <p v-if="serviceMessage" class="settings-message" role="alert">{{ serviceMessage }}</p>
        <footer class="settings-actions">
          <span class="settings-save-state" aria-live="polite"><UIcon :name="dirty ? 'i-lucide-pencil-line' : 'i-lucide-circle-check'" aria-hidden="true" />{{ dirty ? 'بدلونونه لا نه دي خوندي شوي' : 'ټول معلومات تازه دي' }}</span>
          <div class="settings-buttons">
            <UButton type="button" color="neutral" variant="outline" size="lg" icon="i-lucide-rotate-ccw" :disabled="!canReset" @click="confirmReset">بیا تنظیمول</UButton>
            <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending && !resetOpen" :disabled="busy || !dirty" >بدلونونه خوندي کړئ</UButton>
          </div>
        </footer>
      </UForm>
    </section>

    <UModal :open="resetOpen" title="لومړني حالت ته بېرته ګرځول" description="ستاسې خوندي شوي د ډاکټر معلومات او درې واړه انځورونه به پاک شي، او فورمه به تشه شي. دا بدلون بېرته نه شي راګرځېدای." :dismissible="!pending" :close="pending ? false : { 'aria-label': 'تړل' }" :content="{ dir: 'rtl' }" :ui="{ content: 'min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-8 break-words' }" @update:open="value => { if (!pending) resetOpen = value }">
      <template #body><p>ټول معلومات لومړني حالت ته راوګرځوو؟</p><p v-if="serviceMessage" class="settings-message" role="alert">{{ serviceMessage }}</p></template>
      <template #footer>
        <div class="settings-reset-actions">
          <UButton type="button" size="lg" icon="i-lucide-rotate-ccw" :loading="pending" :disabled="pending" @click="reset">هو، بیا تنظیمول</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="resetOpen = false">بندول</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<style scoped>
.settings-page { width: 100%; min-width: 0; max-width: 1100px; margin-inline: auto; }
.settings-page .page-heading p { font-size: 1rem; }
.doctor-settings { min-width: 0; border-radius: 16px; }
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
  .settings-section-heading { padding: 22px 20px; }
  .doctor-form { padding: 23px 20px 0; }
  .settings-text-grid, .settings-images-grid { grid-template-columns: minmax(0, 1fr); }
  .settings-actions { margin-inline: -20px; padding: 20px; }
  .settings-buttons { width: 100%; }
  .settings-buttons > * { flex: 1 1 170px; justify-content: center; }
}
@media (max-width: 480px) {
  .settings-section-heading { padding: 20px 16px; gap: 12px; }
  .doctor-form { padding: 22px 16px 0; }
  .settings-actions { margin-inline: -16px; padding: 20px 16px; }
  .settings-reset-actions > * { flex: 1 1 160px; justify-content: center; }
}
</style>
