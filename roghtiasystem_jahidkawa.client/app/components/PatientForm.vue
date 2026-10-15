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
const genders = [{ label: 'Male', value: 'Male' }, { label: 'Female', value: 'Female' }, { label: 'Other', value: 'Other' }]

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
  if (!value.name.trim()) errors.push({ name: 'name', message: 'Enter the patient name.' })
  else if (value.name.trim().length > 200) errors.push({ name: 'name', message: 'Name must not exceed 200 characters.' })
  if (value.phone.length > 32) errors.push({ name: 'phone', message: 'Phone must not exceed 32 characters.' })
  if (!genders.some(item => item.value === value.gender)) errors.push({ name: 'gender', message: 'Select a gender.' })
  if (String(value.age).trim() === '' || !Number.isInteger(Number(value.age)) || Number(value.age) < 0 || Number(value.age) > 150) {
    errors.push({ name: 'age', message: 'Age must be a whole number between 0 and 150.' })
  }
  if (value.address.length > 1000) errors.push({ name: 'address', message: 'Address must not exceed 1000 characters.' })
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
      VALIDATION_ERROR: 'Check the form details and try again.',
      NOT_FOUND: 'Patient not found. Refresh the list and try again.',
      FORBIDDEN: 'You do not have permission to make this change.',
      INVALID_ORIGIN: 'Request rejected. Refresh the page and try again.',
      TOO_MANY_REQUESTS: 'Too many attempts. Please try again later.'
    }
    if (status === 401) {
      open.value = false
      toast.add({ title: 'Sign in again', description: 'Your session has expired. Please sign in again.', color: 'warning' })
      try { await auth.refresh() } catch { /* Navigation verifies the session again. */ }
      await nuxtApp.runWithContext(() => navigateTo('/login'))
      return
    }
    serviceMessage.value = messages[code || ''] || (status === 403 ? messages.FORBIDDEN! : status === 429 ? messages.TOO_MANY_REQUESTS! : 'Unable to save information. Please try again.')
    toast.add({ title: 'Patient could not be saved', description: serviceMessage.value, color: 'error' })
  } finally {
    pending.value = false
  }
  if (saved) {
    open.value = false
    toast.add({ title: created ? 'Patient registered' : 'Patient information updated', color: 'success', icon: 'i-lucide-circle-check' })
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
    :title="editing ? 'Edit patient' : 'New patient'"
    description="Enter the patient information."
    :dismissible="!pending"
    :close="pending ? false : { 'aria-label': 'Close' }"
    :content="{ dir: 'ltr' }"
    :ui="{ content: 'max-w-2xl min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }"
    @update:open="value => { if (!pending) open = value }"
  >
    <template #body>
      <UForm ref="patientForm" :state="state" :validate="validate" :disabled="pending" :loading-auto="false" novalidate class="patient-form" @submit="save" @error="focusError">
        <p class="patient-owner">
          <template v-if="patient">Code: <bdi>{{ patient.code }}</bdi> · ID: {{ localNumber(patient.id) }}</template>
          <template v-else>The patient code is generated automatically.</template>
        </p>
        <UFormField name="name" label="Patient name" size="lg" required :ui="{ label: 'text-base' }">
          <UInput v-model="state.name" name="name" dir="auto" :maxlength="200" autocomplete="off" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
        </UFormField>
        <div class="patient-form-grid">
          <UFormField name="phone" label="Phone" hint="Optional" size="lg" :ui="{ label: 'text-base' }">
            <UInput v-model="state.phone" name="phone" type="tel" dir="ltr" :maxlength="32" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
          <UFormField name="age" label="Age (years)" size="lg" required :ui="{ label: 'text-base' }">
            <UInput v-model="state.age" name="age" type="number" inputmode="numeric" :min="0" :max="150" :step="1" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
          <UFormField name="gender" label="Gender" size="lg" required :ui="{ label: 'text-base' }">
            <USelect v-model="state.gender" name="gender" :items="genders" placeholder="Select gender" size="xl" class="w-full" :ui="{ base: 'patient-input' }" />
          </UFormField>
        </div>
        <UFormField name="address" label="Address" hint="Optional" size="lg" :ui="{ label: 'text-base' }">
          <UTextarea v-model="state.address" name="address" dir="auto" :maxlength="1000" :rows="3" size="xl" class="w-full" :ui="{ base: 'patient-input patient-address-input' }" />
        </UFormField>
        <p v-if="serviceMessage" class="patient-message" role="alert">{{ serviceMessage }}</p>
        <div class="patient-form-actions">
          <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending" :disabled="pending">{{ editing ? 'Save changes' : 'Register patient' }}</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="open = false">Cancel</UButton>
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
