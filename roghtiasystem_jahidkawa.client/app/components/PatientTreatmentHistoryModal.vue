<script setup lang="ts">
import type { Patient } from '#shared/types/patient'
import type { Treatment, TreatmentPage, TreatmentSummary } from '#shared/types/treatment'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ patient: Patient }>()
const api = useTreatments()
const auth = useAuth()
const toast = useToast()
const pageSize = 8
const result = ref<TreatmentPage>({ items: [], total: 0, page: 1, pageSize })
const loading = ref(false)
const detailLoadingId = ref<number | null>(null)
const selectedTreatment = ref<Treatment | null>(null)
const errorMessage = ref('')
const totalPages = computed(() => Math.max(1, Math.ceil(result.value.total / pageSize)))
const genders: Record<string, string> = { Male: 'Male', Female: 'Female', Other: 'Other' }
let requestSequence = 0

async function load(page = 1) {
  const request = ++requestSequence
  const patientId = props.patient.id
  loading.value = true
  errorMessage.value = ''
  selectedTreatment.value = null
  try {
    const response = await api.list({ patientId, page, pageSize })
    if (request !== requestSequence || patientId !== props.patient.id) return
    result.value = response
  } catch (failure: unknown) {
    if (request !== requestSequence || patientId !== props.patient.id) return
    const response = failure as { status?: number, statusCode?: number }
    if ((response.statusCode || response.status) === 401) {
      await auth.refresh()
      await navigateTo('/login')
      return
    }
    errorMessage.value = 'Unable to load this patient’s treatment history.'
  } finally {
    if (request === requestSequence) loading.value = false
  }
}

async function showDetails(item: TreatmentSummary) {
  if (detailLoadingId.value !== null) return
  const patientId = props.patient.id
  detailLoadingId.value = item.id
  try {
    selectedTreatment.value = await api.get(item.id)
    if (patientId !== props.patient.id || selectedTreatment.value.patient.id !== patientId) selectedTreatment.value = null
  } catch {
    toast.add({ title: 'Unable to load treatment details', description: 'Please try again.', color: 'error' })
  } finally {
    detailLoadingId.value = null
  }
}

watch(open, visible => {
  if (visible) void load(1)
  else {
    ++requestSequence
    selectedTreatment.value = null
  }
})
watch(() => props.patient.id, () => { if (open.value) void load(1) })
</script>

<template>
  <UModal v-model:open="open" :title="`${patient.name} · treatment history`" description="Previous visits and saved treatment details." :content="{ dir: 'ltr' }" :ui="{ content: 'w-[min(960px,calc(100vw-1.5rem))] max-w-none min-w-0', body: 'min-w-0' }">
    <template #body>
      <div class="history-modal">
        <div class="history-patient"><span class="history-avatar"><UIcon name="i-lucide-user-round" /></span><div><strong><bdi>{{ patient.name }}</bdi></strong><span>{{ patient.code }} <template v-if="patient.phone">· <bdi dir="ltr">{{ patient.phone }}</bdi></template></span></div><span class="history-total">{{ localNumber(result.total) }} visits</span></div>

        <div v-if="errorMessage" class="history-state history-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ errorMessage }}</span><UButton type="button" color="neutral" variant="outline" :loading="loading" @click="load(result.page)">Try again</UButton></div>
        <div v-else-if="loading" class="history-state" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" /><span>Loading treatment history…</span></div>
        <div v-else-if="result.items.length" class="history-list">
          <article v-for="item in result.items" :key="item.id" class="history-visit">
            <div class="visit-main"><div class="visit-heading"><strong>{{ formatDateTime(item.recordedAtUtc) }}</strong><span>Treatment #{{ localNumber(item.id) }}</span></div><div class="visit-summary"><span>BP: <bdi dir="ltr">{{ item.bloodPressure || '—' }}</bdi></span><span>{{ localNumber(item.medicationCount) }} medications</span><span>{{ localNumber(item.clinicalNoteCount) }} clinical notes</span></div></div>
            <UButton type="button" color="neutral" variant="outline" icon="i-lucide-eye" :loading="detailLoadingId === item.id" :disabled="detailLoadingId !== null" @click="showDetails(item)">Details</UButton>
          </article>
          <nav v-if="totalPages > 1" class="history-pagination" aria-label="Treatment history pages"><span>Page {{ localNumber(result.page) }} of {{ localNumber(totalPages) }}</span><div><UButton type="button" color="neutral" variant="outline" icon="i-lucide-chevron-left" aria-label="Previous page" :disabled="result.page <= 1 || loading" @click="load(result.page - 1)" /><UButton type="button" color="neutral" variant="outline" icon="i-lucide-chevron-right" aria-label="Next page" :disabled="result.page >= totalPages || loading" @click="load(result.page + 1)" /></div></nav>
        </div>
        <div v-else class="history-state history-empty"><UIcon name="i-lucide-clipboard-list" /><strong>No previous treatments</strong><span>This patient has no saved treatment history yet.</span></div>

        <section v-if="selectedTreatment" class="history-details" aria-live="polite">
          <header><div><span class="detail-eyebrow">Visit details</span><h3>Treatment #{{ localNumber(selectedTreatment.id) }}</h3></div><UButton type="button" color="neutral" variant="ghost" icon="i-lucide-x" aria-label="Close treatment details" @click="selectedTreatment = null" /></header>
          <dl class="detail-facts"><div><dt>Date and time</dt><dd>{{ formatDateTime(selectedTreatment.recordedAtUtc) }}</dd></div><div><dt>Patient code</dt><dd>{{ selectedTreatment.patient.code }}</dd></div><div><dt>Age / gender</dt><dd>{{ localNumber(selectedTreatment.patient.age) }} years · {{ genders[selectedTreatment.patient.gender] || selectedTreatment.patient.gender }}</dd></div><div><dt>Phone</dt><dd dir="ltr">{{ selectedTreatment.patient.phone || '—' }}</dd></div><div><dt>Blood pressure</dt><dd dir="ltr">{{ selectedTreatment.bloodPressure || '—' }}</dd></div><div><dt>Address</dt><dd><bdi>{{ selectedTreatment.patient.address || '—' }}</bdi></dd></div></dl>
          <div class="detail-group"><h4>Medications and prescription</h4><div v-if="selectedTreatment.prescriptionItems.length" class="detail-table-wrap"><table><thead><tr><th>No.</th><th>Medication</th><th>Type</th><th>Dose</th><th>Frequency</th><th>Duration</th></tr></thead><tbody><tr v-for="medicine in selectedTreatment.prescriptionItems" :key="medicine.id"><td>{{ localNumber(medicine.position) }}</td><td><bdi>{{ medicine.medicationName }}</bdi></td><td>{{ medicine.medicationType }}</td><td>{{ localNumber(medicine.dose) }}</td><td>{{ medicine.frequency }}</td><td>{{ medicine.duration }}</td></tr></tbody></table></div><p v-else class="detail-muted">No medications were prescribed.</p></div>
          <div v-if="selectedTreatment.clinicalNotes.length" class="detail-group"><h4>Clinical notes</h4><p>{{ selectedTreatment.clinicalNotes.map(note => note.text).join(', ') }}</p></div>
          <div v-if="selectedTreatment.additionalInstructionItems.length || selectedTreatment.additionalInstructions" class="detail-group"><h4>Additional instructions</h4><p>{{ [...selectedTreatment.additionalInstructionItems.map(item => item.text), ...(selectedTreatment.additionalInstructions ? [selectedTreatment.additionalInstructions] : [])].join(', ') }}</p></div>
          <div v-if="selectedTreatment.diagnostics?.length || (!selectedTreatment.investigations?.length && selectedTreatment.diagnosticInvestigations.length)" class="detail-group"><h4>Diagnostics</h4><p>{{ (selectedTreatment.diagnostics?.length ? selectedTreatment.diagnostics : selectedTreatment.diagnosticInvestigations).map(item => item.text).join(', ') }}</p></div>
          <div v-if="selectedTreatment.investigations?.length" class="detail-group"><h4>Investigations</h4><p>{{ selectedTreatment.investigations.map(item => item.text).join(', ') }}</p></div>
        </section>
      </div>
    </template>
  </UModal>
</template>

<style scoped>
.history-modal { display: grid; gap: 16px; min-width: 0; }
.history-patient { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border: 1px solid var(--line); border-radius: 14px; background: var(--soft); }
.history-avatar { display: grid; place-items: center; width: 42px; height: 42px; flex: 0 0 auto; color: var(--teal); background: var(--surface); border-radius: 12px; }
.history-patient > div { display: grid; min-width: 0; gap: 3px; }
.history-patient > div span, .history-total, .visit-heading span, .detail-muted { color: var(--muted); font-size: .9rem; }
.history-total { margin-inline-start: auto; white-space: nowrap; }
.history-list { display: grid; gap: 8px; max-height: min(42vh, 410px); overflow: auto; }
.history-visit { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 12px 14px; border: 1px solid var(--line); border-radius: 12px; }
.visit-main { display: grid; min-width: 0; gap: 8px; }
.visit-heading, .visit-summary { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 16px; }
.visit-heading span { border-radius: 999px; background: var(--soft); padding: 3px 8px; }
.visit-summary { color: var(--muted); font-size: .9rem; }
.history-state { display: flex; align-items: center; justify-content: center; gap: 10px; min-height: 120px; color: var(--muted); }
.history-error { color: var(--error, #b42318); }
.history-empty { flex-direction: column; gap: 6px; }
.history-empty :deep(svg) { width: 24px; height: 24px; color: var(--teal); }
.history-empty strong { color: var(--ink); }
.history-pagination { display: flex; align-items: center; justify-content: space-between; padding-block: 4px; color: var(--muted); }
.history-pagination > div { display: flex; gap: 8px; }
.history-details { display: grid; gap: 14px; padding: 16px; border: 1px solid var(--line); border-radius: 14px; background: var(--panel); }
.history-details > header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
.history-details h3, .history-details h4 { margin: 2px 0 0; }
.detail-eyebrow { color: var(--teal); font-size: .8rem; font-weight: 600; text-transform: uppercase; letter-spacing: .04em; }
.detail-facts { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; margin: 0; }
.detail-facts > div { min-width: 0; padding: 10px; border: 1px solid var(--line); border-radius: 10px; }
.detail-facts dt { color: var(--muted); font-size: .82rem; }
.detail-facts dd { margin: 4px 0 0; overflow-wrap: anywhere; }
.detail-group { min-width: 0; }
.detail-group h4 { margin-bottom: 8px; }
.detail-group p { margin: 0; color: var(--muted); white-space: pre-wrap; overflow-wrap: anywhere; }
.detail-table-wrap { max-width: 100%; overflow: auto; }
table { width: 100%; min-width: 580px; border-collapse: collapse; text-align: start; }
th, td { padding: 9px 10px; border-bottom: 1px solid var(--line); text-align: start; }
th { color: var(--muted); font-weight: 500; }
@media (max-width: 640px) {
  .history-visit { align-items: flex-start; flex-direction: column; }
  .history-visit > :deep(button) { width: 100%; }
  .detail-facts { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>
