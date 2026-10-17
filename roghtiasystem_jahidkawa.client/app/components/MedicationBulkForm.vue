<script setup lang="ts">
import type { Medication, MedicationInput } from '#shared/types/medication'

const open = defineModel<boolean>('open', { default: false })
const emit = defineEmits<{ saved: [medications: Medication[]] }>()
const api = useMedications()
const auth = useAuth()
const toast = useToast()
const nuxtApp = useNuxtApp()
const pending = ref(false)
const message = ref('')
let nextRowId = 0
type DraftRow = { id: number, name: string, type: string, quantity: string | number, remarks: string }
const rows = ref<DraftRow[]>([])
const medicationTypes = [
  'Tablets', 'Capsules', 'Syrups', 'Suspensions', 'Solutions', 'Drops', 'Injections', 'Creams', 'Ointments',
  'Gels', 'Lotions', 'Sprays', 'Inhalers', 'Suppositories', 'Powders', 'Granules', 'Patches', 'Infusions (IV fluids)'
]

function createRow(): DraftRow {
  return { id: ++nextRowId, name: '', type: 'Tablets', quantity: '', remarks: '' }
}

function asText(value: unknown): string {
  return value == null ? '' : String(value)
}

function reset() {
  rows.value = [createRow(), createRow()]
  message.value = ''
}

function addRow() {
  if (rows.value.length >= 50) return
  rows.value.push(createRow())
}

function removeRow(rowId: number) {
  if (pending.value) return
  rows.value = rows.value.filter(row => row.id !== rowId)
}

const filledRows = computed(() => rows.value.filter(row => asText(row.name).trim() || asText(row.type).trim() || asText(row.quantity).trim() || asText(row.remarks).trim()))

async function save() {
  if (pending.value) return
  message.value = ''
  const items: MedicationInput[] = []
  for (const [index, row] of filledRows.value.entries()) {
    const name = asText(row.name).trim()
    const type = asText(row.type).trim()
    const quantityText = asText(row.quantity).trim()
    const remarks = asText(row.remarks).trim()
    const quantity = quantityText ? Number(quantityText) : 0
    if (!name || name.length > 200 || !type || !Number.isInteger(quantity) || quantity < 0 || quantity > 2147483647 || remarks.length > 1000) {
      message.value = `Check row ${index + 1}: enter a name, type, and valid whole-number quantity (0 or more).`
      return
    }
    items.push({ name, type, quantity, remarks })
  }
  if (!items.length) {
    message.value = 'Enter at least one medication before saving.'
    return
  }
  const seenNames = new Map<string, number>()
  for (const [index, item] of items.entries()) {
    const normalizedName = item.name.trim().toLocaleLowerCase('en-US')
    const previousRow = seenNames.get(normalizedName)
    if (previousRow !== undefined) {
      message.value = `Rows ${previousRow} and ${index + 1} use the same medication name.`
      return
    }
    seenNames.set(normalizedName, index + 1)
  }

  pending.value = true
  try {
    const saved = await api.createBulk(items)
    open.value = false
    toast.add({ title: `${saved.length} medications added`, color: 'success', icon: 'i-lucide-circle-check' })
    emit('saved', saved)
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    const status = failure.statusCode || failure.status
    const code = failure.data?.data?.code || failure.data?.code
    if (status === 401) {
      open.value = false
      toast.add({ title: 'Sign in again', description: 'Your session has expired. Please sign in again.', color: 'warning' })
      try { await auth.refresh() } catch { /* Navigation verifies the session again. */ }
      await nuxtApp.runWithContext(() => navigateTo('/login'))
      return
    }
    message.value = code === 'DUPLICATE_MEDICATION' ? 'A medication with one of these names already exists in your list.'
      : code === 'VALIDATION_ERROR' ? 'Check every medication row and try again.'
      : status === 403 ? 'You do not have permission to add these medications.'
        : status === 429 ? 'Too many attempts. Please try again later.'
          : 'Unable to add medications. Please try again.'
    toast.add({ title: 'Medications could not be added', description: message.value, color: 'error' })
  } finally {
    pending.value = false
  }
}

watch(open, value => {
  if (value) reset()
  else if (!pending.value) message.value = ''
})
</script>

<template>
  <UModal
    :open="open"
    title="Bulk add medications"
    description="Add several medications for your account in one save. Blank rows are ignored."
    :dismissible="!pending"
    :close="pending ? false : { 'aria-label': 'Close' }"
    :content="{ dir: 'ltr' }"
    :ui="{ content: 'max-w-6xl min-w-0', wrapper: 'min-w-0 pe-8', body: 'min-w-0 min-h-0', title: 'text-lg break-words', description: 'text-base leading-7 break-words' }"
    @update:open="value => { if (!pending) open = value }"
  >
    <template #body>
      <div class="bulk-medication-form">
        <div class="bulk-medication-owner"><UIcon name="i-lucide-user-round" aria-hidden="true" /><span>All medications will belong to <bdi>{{ auth.user.value?.userName }}</bdi>.</span><span>{{ localNumber(filledRows.length) }} ready</span></div>
        <div class="bulk-medication-fields" aria-hidden="true"><span>#</span><span>Medication name</span><span>Medication type</span><span>Quantity</span><span>Remarks</span><span /></div>
        <div class="bulk-medication-rows">
          <div v-for="(row, index) in rows" :key="row.id" class="bulk-medication-row">
            <span class="bulk-medication-number">{{ localNumber(index + 1) }}</span>
            <UInput v-model="row.name" :aria-label="`Row ${index + 1} medication name`" :maxlength="200" placeholder="Medication name" autocomplete="off" size="lg" />
            <USelectMenu v-model="row.type" :items="medicationTypes" :aria-label="`Row ${index + 1} medication type`" searchable searchable-placeholder="Search types" placeholder="Select type" size="lg" />
            <UInput v-model="row.quantity" :aria-label="`Row ${index + 1} quantity`" type="number" inputmode="numeric" min="0" max="2147483647" step="1" placeholder="0" size="lg" />
            <UInput v-model="row.remarks" :aria-label="`Row ${index + 1} remarks`" :maxlength="1000" placeholder="Optional" size="lg" />
            <UButton type="button" color="error" variant="ghost" icon="i-lucide-trash-2" :aria-label="`Remove unsaved row ${index + 1}`" :disabled="pending" @click="removeRow(row.id)" />
          </div>
        </div>
        <div class="bulk-medication-actions">
          <UButton type="button" color="neutral" variant="outline" icon="i-lucide-list-plus" :disabled="pending || rows.length >= 50" @click="addRow">Add row</UButton>
          <span class="bulk-medication-limit">Up to 50 medications per batch</span>
        </div>
        <p v-if="message" class="bulk-medication-message" role="alert">{{ message }}</p>
        <div class="bulk-medication-footer">
          <UButton type="button" color="neutral" variant="outline" :disabled="pending" @click="reset">Reset rows</UButton>
          <div><UButton type="button" color="neutral" variant="outline" :disabled="pending" @click="open = false">Cancel</UButton><UButton type="button" icon="i-lucide-save" :loading="pending" :disabled="pending" @click="save">Save medications</UButton></div>
        </div>
      </div>
    </template>
  </UModal>
</template>

<style scoped>
.bulk-medication-form { display: flex; flex-direction: column; gap: 16px; min-width: 0; }
.bulk-medication-owner { display: flex; flex-wrap: wrap; align-items: center; gap: 9px; padding: 12px 14px; border-radius: 10px; background: var(--soft); font-size: 0.95rem; }
.bulk-medication-owner > .iconify { color: var(--teal); }
.bulk-medication-owner > span:last-child { margin-inline-start: auto; color: var(--muted); }
.bulk-medication-fields, .bulk-medication-row { display: grid; grid-template-columns: 38px minmax(140px, 1.2fr) minmax(150px, 1fr) minmax(90px, 0.55fr) minmax(130px, 1fr) 40px; gap: 10px; align-items: center; min-width: 0; }
.bulk-medication-fields { padding: 0 10px; color: var(--muted); font-size: 0.82rem; font-weight: 600; }
.bulk-medication-rows { display: grid; gap: 10px; max-height: min(52vh, 560px); overflow: auto; padding: 2px; }
.bulk-medication-row { padding: 10px; border: 1px solid var(--line); border-radius: 10px; }
.bulk-medication-row > * { min-width: 0; }
.bulk-medication-row :deep(input), .bulk-medication-row :deep(button) { min-height: 42px; }
.bulk-medication-row > button { width: 40px; justify-content: center; }
.bulk-medication-number { color: var(--muted); text-align: center; font-variant-numeric: tabular-nums; }
.bulk-medication-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.bulk-medication-actions button, .bulk-medication-footer button { min-height: 44px; }
.bulk-medication-limit { color: var(--muted); font-size: 0.85rem; }
.bulk-medication-message { margin: 0; padding: 11px 13px; border-radius: 9px; background: var(--soft); color: var(--ink); line-height: 1.7; overflow-wrap: anywhere; }
.bulk-medication-footer, .bulk-medication-footer > div { display: flex; flex-wrap: wrap; gap: 10px; }
.bulk-medication-footer { justify-content: space-between; padding-top: 4px; }
@media (max-width: 760px) {
  .bulk-medication-fields { display: none; }
  .bulk-medication-rows { max-height: 55vh; }
  .bulk-medication-row { grid-template-columns: 30px minmax(0, 1fr) minmax(0, 1fr) 40px; }
  .bulk-medication-row > :nth-child(2), .bulk-medication-row > :nth-child(3) { grid-row: 1; }
  .bulk-medication-row > :nth-child(4), .bulk-medication-row > :nth-child(5) { grid-row: 2; }
  .bulk-medication-number { grid-column: 1; grid-row: 1 / span 2; }
  .bulk-medication-row > :nth-child(6) { grid-column: 4; grid-row: 1 / span 2; }
  .bulk-medication-footer { align-items: stretch; }
  .bulk-medication-footer > *, .bulk-medication-footer > div { flex: 1 1 100%; }
  .bulk-medication-footer > div > * { flex: 1 1 140px; justify-content: center; }
}
@media (max-width: 430px) {
  .bulk-medication-row { grid-template-columns: 26px minmax(0, 1fr) 40px; }
  .bulk-medication-row > :nth-child(2) { grid-column: 2; grid-row: 1; }
  .bulk-medication-row > :nth-child(3) { grid-column: 2; grid-row: 2; }
  .bulk-medication-row > :nth-child(4) { grid-column: 2; grid-row: 3; }
  .bulk-medication-row > :nth-child(5) { grid-column: 2; grid-row: 4; }
  .bulk-medication-row > :nth-child(6) { grid-column: 3; grid-row: 1 / span 4; }
  .bulk-medication-number { grid-column: 1; grid-row: 1 / span 4; align-self: start; padding-top: 11px; }
}
</style>
