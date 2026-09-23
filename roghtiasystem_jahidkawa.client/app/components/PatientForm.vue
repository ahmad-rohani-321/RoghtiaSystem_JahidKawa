<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'
import type { Patient, PatientInput } from '#shared/types/patient'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ patient: Patient | null }>()
const emit = defineEmits<{ saved: [patient: Patient, created: boolean] }>()
const api = usePatients()
const auth = useAuth()
const toast = useToast()
const nuxtApp = useNuxtApp()
const form = useTemplateRef('patientForm')
const pending = ref(false)
const serviceMessage = ref('')
const state = reactive({ name: '', phone: '', age: '' as number | string, gender: '', address: '' })
const editing = computed(() => props.patient !== null)
const genders = [{ label: 'نارینه', value: 'Male' }, { label: 'ښځینه', value: 'Female' }, { label: 'نور', value: 'Other' }]

function populate() {
  Object.assign(state, {
    name: props.patient?.name ?? '',
    phone: props.patient?.phone ?? '',
    age: props.patient?.age ?? '',
    gender: props.patient?.gender ?? '',
    address: props.patient?.address ?? ''
  })
  serviceMessage.value = ''
  form.value?.clear()
}

function validate(value: typeof state): FormError[] {
  const errors: FormError[] = []
  if (!value.name.trim()) errors.push({ name: 'name', message: 'د ناروغ نوم ولیکئ.' })
  else if (value.name.trim().length > 200) errors.push({ name: 'name', message: 'نوم باید تر 200 تورو ډېر نه وي.' })
  if (value.phone.length > 32) errors.push({ name: 'phone', message: 'ټیلیفون باید تر 32 تورو ډېر نه وي.' })
  if (!genders.some(item => item.value === value.gender)) errors.push({ name: 'gender', message: 'جنسیت وټاکئ.' })
  if (String(value.age).trim() === '' || !Number.isInteger(Number(value.age)) || Number(value.age) < 0 || Number(value.age) > 150) {
    errors.push({ name: 'age', message: 'عمر باید د 0 او 150 ترمنځ بشپړ شمېر وي.' })
  }
  if (value.address.length > 1000) errors.push({ name: 'address', message: 'پته باید تر 1000 تورو ډېره نه وي.' })
  return errors
}

function focusError(event: FormErrorEvent) {
  if (event.errors[0]?.id) document.getElementById(event.errors[0].id)?.focus()
}

async function save() {
  if (pending.value) return
  pending.value = true
  serviceMessage.value = ''
  const created = !editing.value
  const input: PatientInput = { name: state.name.trim(), phone: state.phone.trim(), age: Number(state.age), gender: state.gender, address: state.address.trim() }
  let saved: Patient | null = null
  try {
    saved = props.patient ? await api.update(props.patient.id, input) : await api.create(input)
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    const status = failure.statusCode || failure.status
    const code = failure.data?.data?.code || failure.data?.code
    const messages: Record<string, string> = {
      VALIDATION_ERROR: 'د فورمې معلومات وګورئ او بیا هڅه وکړئ.',
      NOT_FOUND: 'دا ناروغ ونه موندل شو. لېست تازه کړئ او بیا هڅه وکړئ.',
      FORBIDDEN: 'تاسې د دې بدلون اجازه نه لرئ.',
      INVALID_ORIGIN: 'غوښتنه ونه منل شوه. پاڼه تازه کړئ او بیا هڅه وکړئ.',
      TOO_MANY_REQUESTS: 'ډېرې هڅې شوې دي. لږ وروسته بیا هڅه وکړئ.'
    }
    if (status === 401) {
      open.value = false
      toast.add({ title: 'بیا ننوځئ', description: 'ستاسې ناسته پای ته رسېدلې ده. بیا خپل حساب ته ننوځئ.', color: 'warning' })
      try { await auth.refresh() } catch { /* Navigation verifies the session again. */ }
      await nuxtApp.runWithContext(() => navigateTo('/login'))
      return
    }
    serviceMessage.value = messages[code || ''] || (status === 403 ? messages.FORBIDDEN! : status === 429 ? messages.TOO_MANY_REQUESTS! : 'معلومات خوندي نه شول. مهرباني وکړئ بیا هڅه وکړئ.')
    toast.add({ title: 'ناروغ خوندي نه شو', description: serviceMessage.value, color: 'error' })
  } finally {
    pending.value = false
  }
  if (saved) {
    open.value = false
    toast.add({ title: created ? 'ناروغ ثبت شو' : 'د ناروغ معلومات تازه شول', color: 'success', icon: 'i-lucide-circle-check' })
    emit('saved', saved, created)
  }
}

watch(open, (value) => {
  if (value) populate()
  else {
    Object.assign(state, { name: '', phone: '', age: '', gender: '', address: '' })
    serviceMessage.value = ''
    form.value?.clear()
  }
})
</script>

<template>
  <UModal
    :open="open"
    :title="editing ? 'د ناروغ سمون' : 'نوی ناروغ'"
    description="د ناروغ اړوند معلومات ولیکئ."
    :dismissible="!pending"
    :close="pending ? false : { 'aria-label': 'تړل' }"
    :content="{ dir: 'rtl' }"
    :ui="{ content: 'max-w-2xl min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }"
    @update:open="value => { if (!pending) open = value }"
  >
    <template #body>
      <UForm ref="patientForm" :state="state" :validate="validate" :disabled="pending" :loading-auto="false" novalidate class="patient-form" @submit="save" @error="focusError">
        <p class="patient-owner">
          <template v-if="patient">کوډ: <bdi>{{ patient.code }}</bdi> · شمېره: {{ localNumber(patient.id) }}</template>
          <template v-else>د ناروغ کوډ په اتومات ډول جوړېږي.</template>
        </p>
        <UFormField name="name" label="د ناروغ نوم" size="lg" required :ui="{ label: 'text-base' }">
          <UInput v-model="state.name" name="name" dir="auto" :maxlength="200" autocomplete="off" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
        </UFormField>
        <div class="patient-form-grid">
          <UFormField name="phone" label="ټیلیفون" hint="اختیاري" size="lg" :ui="{ label: 'text-base' }">
            <UInput v-model="state.phone" name="phone" type="tel" dir="ltr" :maxlength="32" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
          <UFormField name="age" label="عمر (کلونه)" size="lg" required :ui="{ label: 'text-base' }">
            <UInput v-model="state.age" name="age" type="number" inputmode="numeric" :min="0" :max="150" :step="1" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
          <UFormField name="gender" label="جنسیت" size="lg" required :ui="{ label: 'text-base' }">
            <USelect v-model="state.gender" name="gender" :items="genders" placeholder="جنسیت وټاکئ" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
        </div>
        <UFormField name="address" label="پته" hint="اختیاري" size="lg" :ui="{ label: 'text-base' }">
          <UTextarea v-model="state.address" name="address" dir="auto" :maxlength="1000" :rows="3" size="xl" class="w-full" :ui="{ base: 'patient-input patient-address-input' }" />
        </UFormField>
        <p v-if="serviceMessage" class="patient-message" role="alert">{{ serviceMessage }}</p>
        <div class="patient-form-actions">
          <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending" :disabled="pending">{{ editing ? 'بدلونونه خوندي کړئ' : 'ناروغ ثبت کړئ' }}</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="open = false">بندول</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<style scoped>
.patient-form { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.patient-form > *, .patient-form-grid > * { min-width: 0; }
.patient-owner { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 12px 14px; background: var(--soft); color: var(--ink); border-radius: 10px; font-size: 0.875rem; overflow-wrap: anywhere; }
.patient-owner > .iconify { flex-shrink: 0; color: var(--teal); }
.patient-reference { margin-inline-start: auto; }
.patient-form-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 20px; }
.patient-form :deep(.patient-input) { min-width: 0; min-height: 48px; font-size: 1rem; border-radius: 9px; }
.patient-form :deep(.patient-address-input) { resize: vertical; }
.patient-message { padding: 12px 14px; border-radius: 9px; background: var(--soft); font-size: 1rem; line-height: 1.9; margin: 0; overflow-wrap: anywhere; }
.patient-form-actions { display: flex; flex-wrap: wrap; gap: 10px; padding-top: 4px; }
.patient-form-actions :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; font-size: 1rem; white-space: normal; }
@media (max-width: 480px) {
  .patient-form-grid { grid-template-columns: minmax(0, 1fr); }
  .patient-form-actions > * { flex: 1 1 160px; justify-content: center; }
}
</style>
