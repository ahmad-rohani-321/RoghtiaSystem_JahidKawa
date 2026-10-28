<script setup lang="ts">
import type { Patient } from '#shared/types/patient'
import type { Treatment, TreatmentSummary } from '#shared/types/treatment'

const route = useRoute()
const patientId = Number(route.params.id)
if (!Number.isInteger(patientId) || patientId < 1 || patientId > 2147483647) throw createError({ statusCode: 404 })
const patientsApi = usePatients()
const treatmentsApi = useTreatments()
const patient = ref<Patient | null>(null)
const visits = ref<TreatmentSummary[]>([])
const details = reactive<Record<number, Treatment>>({})
const expandedVisit = ref<number | null>(null)
const loading = ref(true)
const loadingDetails = ref<number[]>([])
const printing = ref(false)
const printTimestamp = ref('')
const errorMessage = ref('')
const printError = ref('')
const genders: Record<string, string> = { Male: 'Male', Female: 'Female', Other: 'Other' }

useHead(() => ({ title: patient.value ? patient.value.name + ' — Patient history — E-Roghtia' : 'Patient history — E-Roghtia' }))

async function loadHistory() {
  loading.value = true
  errorMessage.value = ''
  try {
    // Both endpoints enforce patient and treatment ownership for the current user.
    patient.value = await patientsApi.get(patientId)
    const allVisits: TreatmentSummary[] = []
    let page = 1
    let total = 0
    do {
      const response = await treatmentsApi.list({ patientId, page, pageSize: 100 })
      if (response.items.length === 0) break
      allVisits.push(...response.items)
      total = response.total
      page = response.page + 1
    } while (allVisits.length < total)
    visits.value = allVisits
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) {
      await navigateTo({ path: '/login', query: { redirect: route.fullPath } })
      return
    }
    errorMessage.value = status === 404
      ? 'This patient was not found or is not available to your account.'
      : 'Patient history could not be loaded. Check that the server is running, then try again.'
  } finally {
    loading.value = false
  }
}

async function loadDetails(id: number) {
  if (details[id] || loadingDetails.value.includes(id)) return
  loadingDetails.value = [...loadingDetails.value, id]
  try {
    const treatment = await treatmentsApi.get(id)
    if (treatment.patient.id !== patientId) throw new Error('Treatment does not belong to this patient.')
    details[id] = treatment
  } catch {
    printError.value = 'Some treatment details could not be loaded. Refresh the history and try again.'
  } finally {
    loadingDetails.value = loadingDetails.value.filter(value => value !== id)
  }
}

async function toggleVisit(id: number) {
  expandedVisit.value = expandedVisit.value === id ? null : id
  if (expandedVisit.value === id) await loadDetails(id)
}

async function printHistory() {
  if (!patient.value || loading.value || printing.value) return
  printTimestamp.value = formatDateTime(new Date().toISOString())
  printing.value = true
  printError.value = ''
  try {
    for (let index = 0; index < visits.value.length; index += 4) {
      await Promise.all(visits.value.slice(index, index + 4).map(visit => loadDetails(visit.id)))
      if (printError.value) throw new Error(printError.value)
    }
    await nextTick()
    await document.fonts.ready
    await new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve())))
    window.print()
  } catch {
    printError.value = 'Unable to prepare the complete history for printing. Please try again.'
  } finally {
    printing.value = false
  }
}

onMounted(loadHistory)
</script>

<template>
  <section class="history-page" aria-labelledby="history-title" :aria-busy="loading || printing">
    <header class="history-toolbar">
      <div>
        <div class="eyebrow"><span /> Patient record</div>
        <h1 id="history-title">Medical history</h1>
        <p v-if="patient">Complete visit and prescription history for <strong><bdi>{{ patient.name }}</bdi></strong>.</p>
        <p v-else>Patient visits, prescriptions, and treatment notes.</p>
      </div>
      <div class="history-actions">
        <UButton to="/patients" color="neutral" variant="outline" icon="i-lucide-arrow-left">Back to patients</UButton>
        <UButton v-if="patient" type="button" icon="i-lucide-printer" :loading="printing" :disabled="loading || loadingDetails.length > 0" @click="printHistory">Print history</UButton>
      </div>
    </header>
    <div v-if="loading" class="history-state" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" />Loading patient history…</div>
    <div v-else-if="errorMessage" class="history-state history-error" role="alert"><UIcon name="i-lucide-circle-alert" /><p>{{ errorMessage }}</p><UButton type="button" color="neutral" variant="outline" icon="i-lucide-refresh-cw" @click="loadHistory">Try again</UButton></div>
    <template v-else-if="patient">
      <div v-if="printError" class="history-alert" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ printError }}</span></div>
      <article class="history-sheet" aria-label="Patient medical history">
        <header class="patient-summary">
          <div class="patient-avatar" aria-hidden="true"><UIcon name="i-lucide-user-round" /></div>
          <div class="patient-heading"><p>Patient</p><h2><bdi>{{ patient.name }}</bdi></h2></div>
          <div class="patient-code"><span>Patient code</span><strong dir="ltr"><bdi>{{ patient.code }}</bdi></strong></div>
          <dl class="patient-demographics">
            <div><dt>Age</dt><dd>{{ localNumber(patient.age) }} years</dd></div>
            <div><dt>Gender</dt><dd>{{ genders[patient.gender] || patient.gender }}</dd></div>
            <div><dt>Phone</dt><dd dir="ltr"><bdi>{{ patient.phone || '—' }}</bdi></dd></div>
            <div><dt>Address</dt><dd><bdi>{{ patient.address || '—' }}</bdi></dd></div>
          </dl>
        </header>
        <section class="visit-section" aria-labelledby="visits-title">
          <header class="section-heading"><div><span class="section-icon"><UIcon name="i-lucide-notebook-tabs" /></span><div><h2 id="visits-title">Visit history</h2><p>{{ localNumber(visits.length) }} {{ visits.length === 1 ? 'visit' : 'visits' }} on record</p></div></div></header>
          <div v-if="visits.length" class="visit-list">
            <article v-for="(visit, index) in visits" :key="visit.id" class="visit-card">
              <button class="visit-toggle" type="button" :aria-expanded="expandedVisit === visit.id" :aria-controls="'visit-detail-' + visit.id" @click="toggleVisit(visit.id)">
                <span class="visit-number">{{ localNumber(visits.length - index) }}</span>
                <span class="visit-summary"><strong>{{ formatDateTime(visit.recordedAtUtc) }}</strong><small>{{ localNumber(visit.medicationCount) }} medications <span aria-hidden="true">·</span> BP {{ visit.bloodPressure || '—' }}</small></span>
                <UIcon :name="expandedVisit === visit.id ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" class="visit-chevron" aria-hidden="true" />
              </button>
              <div v-if="expandedVisit === visit.id || printing" :id="'visit-detail-' + visit.id" class="visit-detail">
                <div v-if="loadingDetails.includes(visit.id)" class="detail-loading" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" />Loading visit details…</div>
                <template v-else-if="details[visit.id]">
                  <dl class="visit-vitals"><div><dt>Blood pressure</dt><dd>{{ details[visit.id]!.bloodPressure || '—' }}</dd></div><div><dt>Recorded</dt><dd>{{ formatDateTime(details[visit.id]!.recordedAtUtc) }}</dd></div></dl>
                  <section v-if="details[visit.id]!.prescriptionItems.length" class="detail-group"><h3><UIcon name="i-lucide-capsule" /> Medications</h3><div class="medication-table-wrap"><table class="medication-table"><thead><tr><th>#</th><th>Medication</th><th>Type</th><th>Dose</th><th>Frequency</th><th>Duration</th></tr></thead><tbody><tr v-for="item in details[visit.id]!.prescriptionItems" :key="item.id"><td>{{ localNumber(item.position) }}</td><td><bdi>{{ item.medicationName }}</bdi></td><td>{{ item.medicationType }}</td><td>{{ localNumber(item.dose) }}</td><td>{{ item.frequency }}</td><td>{{ item.duration }}</td></tr></tbody></table></div></section>
                  <section v-if="details[visit.id]!.clinicalNotes.length" class="detail-group"><h3><UIcon name="i-lucide-clipboard-list" /> Clinical notes</h3><p>{{ details[visit.id]!.clinicalNotes.map(item => item.text).join(', ') }}</p></section>
                  <section v-if="details[visit.id]!.additionalInstructionItems.length || details[visit.id]!.additionalInstructions" class="detail-group"><h3><UIcon name="i-lucide-info" /> Additional instructions</h3><p>{{ [...details[visit.id]!.additionalInstructionItems.map(item => item.text), ...(details[visit.id]!.additionalInstructions ? [details[visit.id]!.additionalInstructions] : [])].join(', ') }}</p></section>
                  <section v-if="details[visit.id]!.diagnostics.length" class="detail-group"><h3><UIcon name="i-lucide-microscope" /> Diagnostics</h3><p>{{ details[visit.id]!.diagnostics.map(item => item.text).join(', ') }}</p></section>
                  <section v-if="details[visit.id]!.investigations.length" class="detail-group"><h3><UIcon name="i-lucide-microscope" /> Investigations</h3><p>{{ details[visit.id]!.investigations.map(item => item.text).join(', ') }}</p></section>
                  <UButton :to="'/treatments/' + visit.id + '/print'" color="neutral" variant="outline" icon="i-lucide-file-text" class="visit-prescription-link">Open prescription</UButton>
                </template>
              </div>
            </article>
          </div>
          <div v-else class="history-empty"><span><UIcon name="i-lucide-calendar-x" /></span><h3>No visit history yet</h3><p>Treatments recorded for this patient will appear here.</p></div>
        </section>
        <footer class="history-print-footer"><span>E-Roghtia · Patient medical history</span><span v-if="printTimestamp">Printed {{ printTimestamp }}</span></footer>
      </article>
    </template>
  </section>
</template>

<style scoped>
.history-page { width: 100%; max-width: 1120px; min-width: 0; margin-inline: auto; }
.history-toolbar { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 20px; margin-bottom: 24px; }
.history-toolbar > div:first-child { min-width: 0; }
.history-toolbar h1 { margin: 8px 0 4px; font-size: clamp(1.65rem, 3vw, 2.1rem); line-height: 1.25; }
.history-toolbar p { margin: 0; color: var(--muted); line-height: 1.7; }
.history-toolbar p strong { color: var(--ink); }
.history-actions { display: flex; flex-wrap: wrap; gap: 10px; }
.history-actions :deep(button), .history-actions :deep(a) { min-height: 44px; }
.history-state { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 14px; min-height: 260px; padding: 30px; color: var(--muted); text-align: center; }
.history-state > .iconify { width: 24px; height: 24px; color: var(--teal); }
.history-state p { flex-basis: 100%; margin: 0; }
.history-error { padding: 22px; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); }
.history-alert { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; padding: 14px 16px; border-radius: 10px; background: var(--soft); color: var(--ink); }
.history-alert .iconify { flex: 0 0 auto; color: var(--teal); }
.history-sheet { overflow: hidden; border: 1px solid var(--line); border-radius: 18px; background: var(--surface); box-shadow: 0 12px 36px rgb(25 58 76 / 6%); }
.patient-summary { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 12px 16px; padding: 24px; border-bottom: 1px solid var(--line); background: linear-gradient(115deg, var(--soft), var(--surface) 75%); }
.patient-avatar { display: grid; place-items: center; width: 54px; height: 54px; border-radius: 16px; background: var(--surface); color: var(--teal); }
.patient-avatar .iconify { width: 26px; height: 26px; }
.patient-heading p, .patient-code span { margin: 0 0 3px; color: var(--muted); font-size: .875rem; }
.patient-heading h2 { margin: 0; font-size: 1.3rem; line-height: 1.5; overflow-wrap: anywhere; }
.patient-code { display: grid; justify-items: end; }
.patient-code strong { padding: 5px 10px; border-radius: 8px; background: var(--surface); color: var(--teal); font-variant-numeric: tabular-nums; }
.patient-demographics { display: grid; grid-column: 1 / -1; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; margin: 8px 0 0; padding-top: 17px; border-top: 1px solid var(--line); }
.patient-demographics > div { min-width: 0; }
.patient-demographics dt, .visit-vitals dt { margin-bottom: 4px; color: var(--muted); font-size: .825rem; }
.patient-demographics dd, .visit-vitals dd { margin: 0; overflow-wrap: anywhere; }
.visit-section { padding: 24px; }
.section-heading { display: flex; justify-content: space-between; margin-bottom: 18px; }
.section-heading > div { display: flex; align-items: center; gap: 12px; }
.section-icon { display: grid; place-items: center; width: 44px; height: 44px; border-radius: 13px; background: var(--soft); color: var(--teal); }
.section-icon .iconify { width: 22px; height: 22px; }
.section-heading h2 { margin: 0 0 2px; font-size: 1.125rem; }
.section-heading p { margin: 0; color: var(--muted); font-size: .9rem; }
.visit-list { display: grid; gap: 12px; }
.visit-card { min-width: 0; overflow: hidden; border: 1px solid var(--line); border-radius: 13px; background: var(--surface); }
.visit-toggle { display: flex; align-items: center; width: 100%; min-height: 72px; gap: 14px; padding: 14px 16px; border: 0; background: transparent; color: inherit; text-align: start; cursor: pointer; }
.visit-toggle:hover { background: var(--canvas); }
.visit-number { display: grid; place-items: center; flex: 0 0 38px; width: 38px; height: 38px; border-radius: 12px; background: var(--soft); color: var(--teal); font-weight: 700; font-variant-numeric: tabular-nums; }
.visit-summary { display: grid; flex: 1; min-width: 0; gap: 4px; }
.visit-summary strong { font-size: .98rem; font-variant-numeric: tabular-nums; }
.visit-summary small { color: var(--muted); }
.visit-chevron { flex: 0 0 auto; color: var(--muted); }
.visit-detail { display: grid; gap: 16px; padding: 18px 20px 20px; border-top: 1px solid var(--line); background: color-mix(in srgb, var(--canvas) 55%, var(--surface)); }
.detail-loading { display: flex; align-items: center; gap: 10px; min-height: 70px; color: var(--muted); }
.detail-loading .iconify { color: var(--teal); }
.visit-vitals { display: flex; flex-wrap: wrap; gap: 20px 36px; margin: 0; }
.visit-vitals > div { min-width: 140px; }
.detail-group { min-width: 0; }
.detail-group h3 { display: flex; align-items: center; gap: 8px; margin: 0 0 8px; font-size: .98rem; }
.detail-group h3 .iconify { color: var(--teal); }
.detail-group > p { margin: 0; color: var(--muted); line-height: 1.8; white-space: pre-wrap; overflow-wrap: anywhere; }
.medication-table-wrap { overflow-x: auto; border: 1px solid var(--line); border-radius: 9px; }
.medication-table { width: 100%; min-width: 620px; border-collapse: collapse; text-align: start; font-size: .9rem; }
.medication-table th, .medication-table td { padding: 10px 12px; border-bottom: 1px solid var(--line); }
.medication-table th { background: var(--soft); color: var(--muted); font-weight: 600; }
.medication-table tbody tr:last-child td { border-bottom: 0; }
.medication-table td { overflow-wrap: anywhere; }
.visit-prescription-link { justify-self: start; }
.history-empty { display: grid; justify-items: center; gap: 10px; padding: 55px 20px; text-align: center; }
.history-empty > span { display: grid; place-items: center; width: 56px; height: 56px; border-radius: 17px; background: var(--soft); color: var(--teal); }
.history-empty > span .iconify { width: 26px; height: 26px; }
.history-empty h3, .history-empty p { margin: 0; }
.history-empty p { color: var(--muted); }
.history-print-footer { display: none; }
@media (max-width: 640px) {
  .history-toolbar { align-items: stretch; }
  .history-actions { width: 100%; }
  .history-actions > :deep(a), .history-actions > :deep(button) { flex: 1; justify-content: center; }
  .patient-summary, .visit-section { padding: 18px 16px; }
  .patient-demographics { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .visit-toggle { gap: 10px; padding-inline: 12px; }
  .visit-detail { padding: 16px 13px; }
}
@media print {
  @page { size: A4 portrait; margin: 10mm; }
  :global(html), :global(body), :global(#__nuxt) { width: auto !important; min-height: 0 !important; margin: 0 !important; padding: 0 !important; overflow: visible !important; background: #fff !important; }
  :global(body *) { visibility: hidden !important; }
  .history-page, .history-page * { visibility: visible !important; }
  :global(.app-shell > .topbar), :global(.navigation-shell) { display: none !important; }
  .history-page { display: block !important; position: absolute !important; inset: 0 auto auto 0 !important; width: 100% !important; max-width: none !important; margin: 0 !important; color: #172b37; }
  .history-toolbar, .history-alert, .visit-toggle .visit-chevron, .visit-prescription-link { display: none !important; }
  .history-sheet { display: block !important; overflow: visible; border: 0; border-radius: 0; box-shadow: none; background: #fff; }
  .patient-summary { padding: 0 0 14px; border-bottom: 1px solid #719181; background: #fff; }
  .patient-avatar { display: none; }
  .patient-heading { grid-column: 1 / 3; }
  .patient-heading h2 { font-size: 17pt; }
  .patient-code { grid-column: 3; grid-row: 1; }
  .patient-demographics { grid-template-columns: repeat(4, minmax(0, 1fr)); }
  .visit-section { padding: 16px 0; }
  .section-heading { margin-bottom: 10px; }
  .section-icon { display: none; }
  .visit-list { gap: 10px; }
  .visit-card { overflow: visible; border-color: #cedbd5; border-radius: 7px; break-inside: avoid; }
  .visit-toggle { min-height: 0; padding: 8px 10px; }
  .visit-number { flex-basis: 27px; width: 27px; height: 27px; border-radius: 50%; }
  .visit-detail { display: grid !important; gap: 9px; padding: 10px; border-top: 1px solid #d7e3dd; background: #fff; }
  .detail-loading { display: none !important; }
  .detail-group h3 { margin-bottom: 4px; font-size: 9pt; }
  .detail-group > p, .visit-vitals, .medication-table { font-size: 8.5pt; }
  .medication-table { min-width: 0; }
  .medication-table th, .medication-table td { padding: 5px 6px; }
  .medication-table-wrap { overflow: visible; }
  .history-print-footer { display: flex; justify-content: space-between; gap: 12px; border-top: 1px solid #cedbd5; padding-top: 7px; color: #5b6f67; font-size: 8pt; }
  .history-empty { padding: 22px; }
}
</style>
