<script setup lang="ts">
import type { FormError, FormErrorEvent } from '@nuxt/ui'
import type { Medication, MedicationInput } from '#shared/types/medication'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ medication: Medication | null }>()
const emit = defineEmits<{ saved: [medication: Medication, created: boolean] }>()
const api = useMedications()
const auth = useAuth()
const toast = useToast()
const nuxtApp = useNuxtApp()
const form = useTemplateRef('medicationForm')
const pending = ref(false)
const serviceMessage = ref('')
const state = reactive({ name: '', type: '', quantity: 0 as number | string, remarks: '' })
const editing = computed(() => props.medication !== null)
const medicationTypes = [
  'Tablets',
  'Capsules',
  'Syrups',
  'Suspensions',
  'Solutions',
  'Drops',
  'Injections',
  'Creams',
  'Ointments',
  'Gels',
  'Lotions',
  'Sprays',
  'Inhalers',
  'Suppositories',
  'Powders',
  'Granules',
  'Patches',
  'Infusions (IV fluids)'
]

function populate() {
  Object.assign(state, {
    name: props.medication?.name ?? '',
    type: props.medication?.type ?? '',
    quantity: props.medication?.quantity ?? 0,
    remarks: props.medication?.remarks ?? ''
  })
  serviceMessage.value = ''
  form.value?.clear()
}

function validate(value: typeof state): FormError[] {
  const errors: FormError[] = []
  if (!value.name.trim()) errors.push({ name: 'name', message: 'د درملو نوم ولیکئ.' })
  else if (value.name.trim().length > 200) errors.push({ name: 'name', message: 'نوم باید تر 200 تورو ډېر نه وي.' })
  if (!value.type.trim()) errors.push({ name: 'type', message: 'د درملو ډول ولیکئ.' })
  else if (value.type.trim().length > 100) errors.push({ name: 'type', message: 'ډول باید تر 100 تورو ډېر نه وي.' })
  if (String(value.quantity).trim() === '' || !Number.isInteger(Number(value.quantity)) || Number(value.quantity) < 0 || Number(value.quantity) > 2147483647) {
    errors.push({ name: 'quantity', message: 'مقدار باید د 0 او 2147483647 ترمنځ بشپړ شمېر وي.' })
  }
  if (value.remarks.trim().length > 1000) errors.push({ name: 'remarks', message: 'یادښت باید تر 1000 تورو ډېر نه وي.' })
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
  const input: MedicationInput = { name: state.name.trim(), type: state.type.trim(), quantity: Number(state.quantity), remarks: state.remarks.trim() }
  let saved: Medication | null = null
  try {
    saved = props.medication ? await api.update(props.medication.id, input) : await api.create(input)
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    const status = failure.statusCode || failure.status
    const code = failure.data?.data?.code || failure.data?.code
    const messages: Record<string, string> = {
      VALIDATION_ERROR: 'د فورمې معلومات وګورئ او بیا هڅه وکړئ.',
      NOT_FOUND: 'دا درمل ونه موندل شول. لېست تازه کړئ او بیا هڅه وکړئ.',
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
    toast.add({ title: 'درمل خوندي نه شول', description: serviceMessage.value, color: 'error' })
  } finally {
    pending.value = false
  }
  if (saved) {
    open.value = false
    toast.add({ title: created ? 'نوي درمل ثبت شول' : 'د درملو معلومات تازه شول', color: 'success', icon: 'i-lucide-circle-check' })
    emit('saved', saved, created)
  }
}

watch(open, (value) => {
  if (value) populate()
  else {
    Object.assign(state, { name: '', type: '', quantity: 0, remarks: '' })
    serviceMessage.value = ''
    form.value?.clear()
  }
})
</script>

<template>
  <UModal
    :open="open"
    :title="editing ? 'د درملو سمون' : 'نوي درمل'"
    description="د درملو نوم، ډول او مقدار ولیکئ."
    :dismissible="!pending"
    :close="pending ? false : { 'aria-label': 'تړل' }"
    :content="{ dir: 'rtl' }"
    :ui="{ content: 'max-w-2xl min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }"
    @update:open="value => { if (!pending) open = value }"
  >
    <template #body>
      <UForm ref="medicationForm" :state="state" :validate="validate" :disabled="pending" :loading-auto="false" novalidate class="medication-form" @submit="save" @error="focusError">
        <div class="medication-owner">
          <UIcon name="i-lucide-user-round" aria-hidden="true" />
          <span>کارن: <bdi>{{ auth.user.value?.userName }}</bdi></span>
          <span v-if="medication" class="medication-reference">شمېره: {{ localNumber(medication.id) }}</span>
        </div>
        <UFormField name="name" label="د درملو نوم" size="lg" required :ui="{ label: 'text-base' }">
          <UInput v-model="state.name" name="name" dir="auto" :maxlength="200" placeholder="د درملو نوم ولیکئ" autocomplete="off" size="xl" class="w-full" :ui="{ base: 'medication-input' }" />
        </UFormField>
        <div class="medication-form-grid">
          <UFormField name="type" label="د درملو ډول" size="lg" required :ui="{ label: 'text-base' }">
            <USelectMenu v-model="state.type" :items="medicationTypes" name="type" searchable searchable-placeholder="د درملو ډول ولټوئ" placeholder="د درملو ډول وټاکئ" size="xl" class="w-full" :ui="{ base: 'medication-input' }" />
          </UFormField>
          <UFormField name="quantity" label="مقدار" size="lg" required :ui="{ label: 'text-base' }">
            <UInput v-model="state.quantity" name="quantity" type="number" inputmode="numeric" :min="0" :max="2147483647" :step="1" size="xl" class="w-full" :ui="{ base: 'medication-input' }" />
          </UFormField>
        </div>
        <UFormField name="remarks" label="یادښت" hint="اختیاري" size="lg" :ui="{ label: 'text-base' }">
          <UTextarea v-model="state.remarks" name="remarks" dir="auto" :maxlength="1000" :rows="3" placeholder="د اړتیا په صورت کې نور معلومات ولیکئ" size="xl" class="w-full" :ui="{ base: 'medication-input medication-remarks-input' }" />
        </UFormField>
        <p v-if="serviceMessage" class="medication-message" role="alert">{{ serviceMessage }}</p>
        <div class="medication-form-actions">
          <UButton type="submit" size="lg" icon="i-lucide-save" :loading="pending" :disabled="pending">{{ editing ? 'بدلونونه خوندي کړئ' : 'درمل ثبت کړئ' }}</UButton>
          <UButton type="button" size="lg" color="neutral" variant="outline" :disabled="pending" @click="open = false">بندول</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<style scoped>
.medication-form { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.medication-form > *, .medication-form-grid > * { min-width: 0; }
.medication-owner { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 12px 14px; background: var(--soft); color: var(--ink); border-radius: 10px; font-size: 0.875rem; overflow-wrap: anywhere; }
.medication-owner > .iconify { flex-shrink: 0; color: var(--teal); }
.medication-reference { margin-inline-start: auto; }
.medication-form-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 20px; }
.medication-form :deep(.medication-input) { min-width: 0; min-height: 48px; font-size: 1rem; border-radius: 9px; }
.medication-form :deep(.medication-remarks-input) { resize: vertical; }
.medication-message { padding: 12px 14px; border-radius: 9px; background: var(--soft); font-size: 1rem; line-height: 1.9; margin: 0; overflow-wrap: anywhere; }
.medication-form-actions { display: flex; flex-wrap: wrap; gap: 10px; padding-top: 4px; }
.medication-form-actions :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; font-size: 1rem; white-space: normal; }
@media (max-width: 480px) {
  .medication-form-grid { grid-template-columns: minmax(0, 1fr); }
  .medication-form-actions > * { flex: 1 1 160px; justify-content: center; }
}
</style>
