<script setup lang="ts">
import { parseDate } from '@internationalized/date'
import type { ReportsResponse } from '#shared/types/reports'

useHead({ title: 'Reports — E-Roghtia' })
const api = useRequestFetch()
const auth = useAuth()
const toast = useToast()
const today = jalaliToday()
const period = ref<'week' | 'month' | 'year' | 'custom'>('month')
const fromDate = shallowRef(today.set({ day: 1 }))
const toDate = shallowRef(today)
const data = ref<ReportsResponse | null>(null)
const loading = ref(false)
const error = ref('')
const activePrint = ref('')
const lowStockLimit = 10
const lowStockItems = computed(() => data.value?.medications.filter(item => item.currentQuantity <= lowStockLimit) || [])
const noVisitPatients = computed(() => data.value?.patients.filter(item => item.visits === 0) || [])
const ageBands = computed(() => {
  const bands = [{ label: '0–17', min: 0, max: 17 }, { label: '18–35', min: 18, max: 35 }, { label: '36–59', min: 36, max: 59 }, { label: '60+', min: 60, max: 150 }]
  return bands.map(band => ({ ...band, count: data.value?.patients.filter(patient => patient.age >= band.min && patient.age <= band.max).length || 0 }))
})
const genderCounts = computed(() => ['Male', 'Female', 'Other'].map(gender => ({ gender, count: data.value?.patients.filter(patient => patient.gender === gender).length || 0 })))
const rangeLabel = computed(() => period.value === 'custom' ? `${formatJalali(fromDate.value)} — ${formatJalali(toDate.value)}` : period.value === 'week' ? 'Last 7 days' : period.value === 'year' ? 'Current Jalali year' : 'Current Jalali month')

function reportRange() {
  const endDate = jalaliToday()
  if (period.value === 'week') return { from: endDate.subtract({ days: 6 }), to: endDate }
  if (period.value === 'year') return { from: endDate.set({ month: 1, day: 1 }), to: endDate }
  if (period.value === 'month') return { from: endDate.set({ day: 1 }), to: endDate }
  return { from: fromDate.value, to: toDate.value }
}

async function loadReports() {
  if (loading.value) return
  error.value = ''
  const { from, to } = reportRange()
  if (from.compare(to) > 0) {
    error.value = 'The start date must be on or before the end date.'
    return
  }
  const fromUtc = from.toDate('Asia/Kabul').toISOString()
  const toUtc = to.add({ days: 1 }).toDate('Asia/Kabul').toISOString()
  loading.value = true
  try {
    data.value = await api<ReportsResponse>('/api/reports', { query: { fromUtc, toUtc }, retry: 0 })
  } catch (failure: unknown) {
    const response = failure as { status?: number, statusCode?: number }
    const status = response.statusCode || response.status
    if (status === 401) {
      await auth.refresh()
      await navigateTo('/login')
      return
    }
    error.value = status === 400 ? 'Choose a valid date range of up to one year.' : 'Unable to load reports. Please check the connection and try again.'
  } finally { loading.value = false }
}

async function printReport(id: string) {
  activePrint.value = id
  await nextTick()
  window.print()
}

onMounted(() => window.addEventListener('roghtia:data-changed', loadReports))
onBeforeUnmount(() => window.removeEventListener('roghtia:data-changed', loadReports))
watch(period, () => { if (period.value !== 'custom') void loadReports() })
watch([fromDate, toDate], () => { if (period.value === 'custom') void loadReports() })
await loadReports()
</script>

<template>
  <section class="reports-page" aria-labelledby="reports-title">
    <header class="page-heading reports-heading">
      <div><div class="eyebrow"><span /> Clinic analytics</div><h1 id="reports-title">Reports</h1><p>Review your patients, treatments, medication stock, clinical notes, and instructions.</p></div>
      <UButton type="button" color="neutral" variant="outline" icon="i-lucide-refresh-cw" :loading="loading" @click="loadReports">Refresh reports</UButton>
    </header>

    <section class="panel report-filters" aria-label="Report date range">
      <div class="filter-copy"><span class="filter-icon"><UIcon name="i-lucide-calendar-range" /></span><div><h2>Reporting period</h2><p>{{ rangeLabel }} · Kabul local time</p></div></div>
      <div class="period-options" role="group" aria-label="Choose reporting period">
        <UButton v-for="option in ([['week', '7 days'], ['month', 'This month'], ['year', 'This year'], ['custom', 'Custom']] as const)" :key="option[0]" type="button" :color="period === option[0] ? 'primary' : 'neutral'" :variant="period === option[0] ? 'solid' : 'outline'" :aria-pressed="period === option[0]" @click="period = option[0]">{{ option[1] }}</UButton>
      </div>
      <div v-if="period === 'custom'" class="custom-dates"><label>From <JalaliPicker v-model="fromDate" /></label><label>Through <JalaliPicker v-model="toDate" /></label><UButton type="button" icon="i-lucide-search" :loading="loading" @click="loadReports">Apply</UButton></div>
    </section>

    <div v-if="loading && !data" class="report-state" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" /><span>Preparing your reports…</span></div>
    <div v-else-if="error && !data" class="report-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ error }}</span><UButton type="button" color="neutral" variant="outline" @click="loadReports">Try again</UButton></div>

    <template v-if="data">
      <div v-if="error" class="report-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ error }}</span></div>
      <div class="report-summary" aria-label="Period summary">
        <article class="summary-card"><span class="summary-icon"><UIcon name="i-lucide-stethoscope" /></span><div><p>Treatments</p><strong>{{ localNumber(data.treatments.total) }}</strong></div></article>
        <article class="summary-card"><span class="summary-icon blue"><UIcon name="i-lucide-notebook-pen" /></span><div><p>With prescription</p><strong>{{ localNumber(data.treatments.withPrescription) }}</strong></div></article>
        <article class="summary-card"><span class="summary-icon purple"><UIcon name="i-lucide-users-round" /></span><div><p>Patients</p><strong>{{ localNumber(data.patients.length) }}</strong></div></article>
        <article class="summary-card"><span class="summary-icon orange"><UIcon name="i-lucide-pill" /></span><div><p>Medication types</p><strong>{{ localNumber(new Set(data.medications.map(item => item.type)).size) }}</strong></div></article>
      </div>

      <section class="panel report-section" :class="{ 'print-target': activePrint === 'treatments' }" aria-labelledby="treatment-report-title">
        <header class="report-section-heading"><div><span class="report-section-icon"><UIcon name="i-lucide-activity" /></span><div><h2 id="treatment-report-title">Treatment and prescription report</h2><p>Visit volume and patients seen during {{ rangeLabel.toLowerCase() }}.</p></div></div><UButton class="print-action" type="button" color="neutral" variant="outline" icon="i-lucide-printer" @click="printReport('treatments')">Print / PDF</UButton></header>
        <div class="report-two-column">
          <div><h3>Treatments by day</h3><div v-if="data.treatments.byDay.length" class="daily-bars"><div v-for="day in data.treatments.byDay" :key="day.date" class="daily-row"><time>{{ formatJalali(parseDate(day.date)) }}</time><div class="bar-track"><span :style="{ width: `${Math.max(4, day.treatments / Math.max(...data.treatments.byDay.map(item => item.treatments)) * 100)}%` }" /></div><b>{{ localNumber(day.treatments) }}</b><small>{{ localNumber(day.prescriptions) }} Rx</small></div></div><p v-else class="empty-report">No treatments were recorded in this period.</p></div>
          <div><h3>Most visited patients</h3><div v-if="data.treatments.mostVisitedPatients.length" class="report-table-wrap"><table><thead><tr><th>Patient</th><th>Code</th><th>Visits</th></tr></thead><tbody><tr v-for="patient in data.treatments.mostVisitedPatients" :key="patient.patientId"><td><bdi>{{ patient.patientName }}</bdi></td><td><bdi>{{ patient.patientCode }}</bdi></td><td>{{ localNumber(patient.visits) }}</td></tr></tbody></table></div><p v-else class="empty-report">No patient visits in this period.</p></div>
        </div>
      </section>

      <section class="panel report-section" :class="{ 'print-target': activePrint === 'medications' }" aria-labelledby="medication-report-title">
        <header class="report-section-heading"><div><span class="report-section-icon blue"><UIcon name="i-lucide-pill" /></span><div><h2 id="medication-report-title">Medication and stock report</h2><p>Current balances plus prescription and recorded stock deduction in this period. Low stock means {{ localNumber(lowStockLimit) }} or fewer.</p></div></div><UButton class="print-action" type="button" color="neutral" variant="outline" icon="i-lucide-printer" @click="printReport('medications')">Print / PDF</UButton></header>
        <div class="stock-highlights"><span><b>{{ localNumber(data.medications.length) }}</b> medications</span><span class="warning-text"><b>{{ localNumber(lowStockItems.length) }}</b> low or out of stock</span><span><b>{{ localNumber(data.medications.reduce((sum, item) => sum + item.prescriptionLines, 0)) }}</b> prescribed lines</span></div>
        <div class="report-table-wrap"><table><thead><tr><th>Medication</th><th>Type</th><th>Current quantity</th><th>Prescription lines</th><th>Prescribed dose</th><th>Recorded deducted dose</th></tr></thead><tbody><tr v-for="item in data.medications" :key="item.id"><th scope="row"><bdi>{{ item.name }}</bdi></th><td><bdi>{{ item.type }}</bdi></td><td><span :class="{ 'warning-text': item.currentQuantity <= lowStockLimit }">{{ localNumber(item.currentQuantity) }}</span></td><td>{{ localNumber(item.prescriptionLines) }}</td><td>{{ localNumber(item.prescribedDose) }}</td><td>{{ localNumber(item.deductedDose) }}</td></tr><tr v-if="!data.medications.length"><td colspan="6" class="empty-report">No medications are registered.</td></tr></tbody></table></div>
        <p class="report-footnote">Current quantity is the latest stored balance. Deducted dose includes prescription rows whose stock deduction was recorded; this is not a full stock transaction history.</p>
      </section>

      <section class="panel report-section" :class="{ 'print-target': activePrint === 'patients' }" aria-labelledby="patient-report-title">
        <header class="report-section-heading"><div><span class="report-section-icon purple"><UIcon name="i-lucide-users-round" /></span><div><h2 id="patient-report-title">Patient report</h2><p>Patient directory and visit activity during {{ rangeLabel.toLowerCase() }}.</p></div></div><UButton class="print-action" type="button" color="neutral" variant="outline" icon="i-lucide-printer" @click="printReport('patients')">Print / PDF</UButton></header>
        <div class="demographic-highlights"><div><h3>By gender</h3><p v-for="item in genderCounts" :key="item.gender"><span>{{ item.gender }}</span><b>{{ localNumber(item.count) }}</b></p></div><div><h3>By age</h3><p v-for="band in ageBands" :key="band.label"><span>{{ band.label }} years</span><b>{{ localNumber(band.count) }}</b></p></div><div><h3>Visit status</h3><p><span>Visited this period</span><b>{{ localNumber(data.patients.filter(item => item.visits > 0).length) }}</b></p><p><span>No visit this period</span><b>{{ localNumber(noVisitPatients.length) }}</b></p></div></div>
        <div class="report-table-wrap"><table><thead><tr><th>Code</th><th>Patient</th><th>Phone</th><th>Age</th><th>Gender</th><th>Visits in period</th><th>Address</th></tr></thead><tbody><tr v-for="patient in data.patients" :key="patient.id"><td><bdi>{{ patient.code }}</bdi></td><th scope="row"><bdi>{{ patient.name }}</bdi></th><td><bdi dir="ltr">{{ patient.phone || '—' }}</bdi></td><td>{{ localNumber(patient.age) }}</td><td>{{ patient.gender }}</td><td>{{ localNumber(patient.visits) }}</td><td><bdi>{{ patient.address || '—' }}</bdi></td></tr><tr v-if="!data.patients.length"><td colspan="7" class="empty-report">No patients are registered.</td></tr></tbody></table></div>
        <p class="report-footnote">The model stores patient age but not date of birth or registration date, so this report does not estimate either.</p>
      </section>

      <section class="panel report-section" :class="{ 'print-target': activePrint === 'notes' }" aria-labelledby="notes-report-title">
        <header class="report-section-heading"><div><span class="report-section-icon green"><UIcon name="i-lucide-notebook-pen" /></span><div><h2 id="notes-report-title">Clinical notes report</h2><p>Saved note library ranked by use on treatments in this period.</p></div></div><UButton class="print-action" type="button" color="neutral" variant="outline" icon="i-lucide-printer" @click="printReport('notes')">Print / PDF</UButton></header>
        <div class="report-table-wrap"><table><thead><tr><th>#</th><th>Clinical note</th><th>Uses in period</th></tr></thead><tbody><tr v-for="(item, index) in [...data.clinicalNotes].sort((a, b) => b.uses - a.uses || a.text.localeCompare(b.text))" :key="item.text"><td>{{ localNumber(index + 1) }}</td><th scope="row"><bdi>{{ item.text }}</bdi></th><td>{{ localNumber(item.uses) }}</td></tr><tr v-if="!data.clinicalNotes.length"><td colspan="3" class="empty-report">No clinical notes have been saved.</td></tr></tbody></table></div>
      </section>

      <section class="panel report-section" :class="{ 'print-target': activePrint === 'instructions' }" aria-labelledby="instructions-report-title">
        <header class="report-section-heading"><div><span class="report-section-icon orange"><UIcon name="i-lucide-list-checks" /></span><div><h2 id="instructions-report-title">Additional instructions report</h2><p>Saved instruction library ranked by use on treatments in this period.</p></div></div><UButton class="print-action" type="button" color="neutral" variant="outline" icon="i-lucide-printer" @click="printReport('instructions')">Print / PDF</UButton></header>
        <div class="report-table-wrap"><table><thead><tr><th>#</th><th>Additional instruction</th><th>Uses in period</th></tr></thead><tbody><tr v-for="(item, index) in [...data.additionalInstructions].sort((a, b) => b.uses - a.uses || a.text.localeCompare(b.text))" :key="item.text"><td>{{ localNumber(index + 1) }}</td><th scope="row"><bdi>{{ item.text }}</bdi></th><td>{{ localNumber(item.uses) }}</td></tr><tr v-if="!data.additionalInstructions.length"><td colspan="3" class="empty-report">No additional instructions have been saved.</td></tr></tbody></table></div>
      </section>
      <p class="reports-disclaimer">Reports contain records for your account only. Use “Print / PDF” on a section, then choose a printer or “Save as PDF” in the browser print dialog.</p>
    </template>
  </section>
</template>

<style scoped>
.reports-page { width: 100%; max-width: 1320px; min-width: 0; margin-inline: auto; }
.reports-page .page-heading p { max-width: 760px; font-size: 1rem; line-height: 1.8; }
.reports-heading > button { min-height: 46px; flex-shrink: 0; }
.report-filters { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 18px; margin-bottom: 20px; padding: 20px 22px; }
.filter-copy, .report-section-heading > div { display: flex; align-items: center; gap: 12px; min-width: 0; }
.filter-icon, .report-section-icon, .summary-icon { display: grid; place-items: center; width: 44px; height: 44px; flex: 0 0 auto; border-radius: 12px; color: var(--teal); background: var(--soft); }
.filter-icon .iconify, .report-section-icon .iconify { width: 22px; height: 22px; }
.filter-copy h2, .report-section-heading h2 { margin: 0 0 3px; font-size: 1.1rem; font-weight: 650; }
.filter-copy p, .report-section-heading p { margin: 0; color: var(--muted); font-size: .9rem; line-height: 1.7; }
.period-options { display: flex; flex-wrap: wrap; gap: 7px; }
.period-options > button, .custom-dates > button { min-height: 42px; }
.custom-dates { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.custom-dates label { display: flex; align-items: center; gap: 7px; color: var(--muted); font-size: .9rem; }
.report-state, .report-error { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 12px; min-height: 180px; padding: 22px; color: var(--muted); }
.report-error { justify-content: flex-start; min-height: auto; margin: 0 0 18px; border: 1px solid var(--line); border-radius: 12px; background: var(--surface); }
.report-error > .iconify { color: #c48122; }
.report-summary { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; margin-bottom: 20px; }
.summary-card { display: flex; align-items: center; gap: 13px; min-width: 0; padding: 17px; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); box-shadow: 0 4px 16px #163a3510; }
.summary-icon { width: 40px; height: 40px; }.summary-icon.blue, .report-section-icon.blue { color: #477cab; background: #eaf3fb; }.summary-icon.purple, .report-section-icon.purple { color: #8070ad; background: #f1edfa; }.summary-icon.orange, .report-section-icon.orange { color: #b97e28; background: #fbf4e8; }.report-section-icon.green { color: #3b9270; background: #edf7f1; }
.summary-card p { margin: 0 0 3px; color: var(--muted); font-size: .875rem; }.summary-card strong { font-size: 1.45rem; font-variant-numeric: tabular-nums; }
.report-section { min-width: 0; margin-bottom: 18px; padding: 22px; break-inside: avoid; }
.report-section-heading { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 18px; }
.report-section-heading h2 { font-size: 1.15rem; }.report-section-heading > div > div { min-width: 0; }
.print-action { min-height: 42px; flex-shrink: 0; }
.report-two-column { display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr); gap: 26px; }
.report-two-column h3, .demographic-highlights h3 { margin: 0 0 12px; font-size: 1rem; font-weight: 650; }
.daily-bars { display: grid; gap: 10px; }.daily-row { display: grid; grid-template-columns: 130px minmax(50px, 1fr) 34px 52px; align-items: center; gap: 9px; font-size: .85rem; }.daily-row time { color: var(--muted); }.daily-row b, .daily-row small { text-align: end; font-variant-numeric: tabular-nums; }.daily-row small { color: var(--muted); }.bar-track { height: 10px; overflow: hidden; border-radius: 99px; background: var(--soft); }.bar-track span { display: block; height: 100%; border-radius: inherit; background: var(--teal); }
.report-table-wrap { width: 100%; min-width: 0; overflow: auto; border: 1px solid var(--line); border-radius: 10px; }.report-table-wrap table { width: 100%; min-width: 560px; border-collapse: collapse; text-align: start; }.report-table-wrap th, .report-table-wrap td { padding: 11px 13px; border-bottom: 1px solid var(--line); text-align: start; font-size: .9rem; vertical-align: top; }.report-table-wrap thead { background: var(--canvas); color: var(--muted); }.report-table-wrap tbody tr:last-child > * { border-bottom: 0; }.report-table-wrap tbody tr:hover { background: var(--canvas); }
.stock-highlights { display: flex; flex-wrap: wrap; gap: 10px 20px; margin-bottom: 14px; color: var(--muted); font-size: .9rem; }.stock-highlights b { color: var(--ink); font-variant-numeric: tabular-nums; }.warning-text { color: #b76820 !important; font-weight: 650; }
.demographic-highlights { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; margin-bottom: 18px; }.demographic-highlights > div { min-width: 0; padding: 14px; border: 1px solid var(--line); border-radius: 10px; }.demographic-highlights p { display: flex; justify-content: space-between; gap: 10px; margin: 7px 0 0; color: var(--muted); font-size: .9rem; }.demographic-highlights p b { color: var(--ink); font-variant-numeric: tabular-nums; }
.empty-report { padding: 20px !important; color: var(--muted); text-align: center !important; }.report-footnote, .reports-disclaimer { margin: 12px 0 0; color: var(--muted); font-size: .85rem; line-height: 1.7; }.reports-disclaimer { margin: 8px 0 20px; }
@media (max-width: 900px) { .report-summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }.report-two-column { grid-template-columns: minmax(0, 1fr); }.daily-row { grid-template-columns: 115px minmax(35px, 1fr) 30px 48px; } }
@media (max-width: 600px) { .reports-heading > button { width: 100%; justify-content: center; }.report-filters { align-items: stretch; padding: 17px; }.filter-copy { align-items: flex-start; }.period-options > button { flex: 1 1 40%; justify-content: center; }.custom-dates { align-items: stretch; }.custom-dates label { flex: 1 1 100%; justify-content: space-between; }.custom-dates > button { flex: 1 1 100%; justify-content: center; }.report-summary { gap: 9px; }.summary-card { padding: 13px 11px; gap: 9px; }.summary-icon { width: 34px; height: 34px; }.summary-card strong { font-size: 1.2rem; }.summary-card p { font-size: .78rem; }.report-section { padding: 17px 14px; }.report-section-heading { align-items: flex-start; }.report-section-heading > div { align-items: flex-start; }.print-action { width: 100%; justify-content: center; }.demographic-highlights { grid-template-columns: minmax(0, 1fr); }.daily-row { grid-template-columns: 104px minmax(20px, 1fr) 26px 42px; gap: 6px; font-size: .75rem; }.report-table-wrap table { min-width: 640px; } }
@media print {
  :global(body *) { visibility: hidden !important; }
  .reports-page, .reports-page * { visibility: visible !important; }
  .reports-page { position: absolute; inset: 0; width: 100%; max-width: none; padding: 0; background: white; color: #111; }
  .reports-heading, .report-filters, .report-summary, .reports-disclaimer, .print-action { display: none !important; }
  .report-section:not(.print-target) { display: none !important; }
  .report-section.print-target { display: block !important; margin: 0; padding: 0; border: 0; box-shadow: none; color: #111; }
  .report-section.print-target :deep(*) { color: #111; }
  .report-section.print-target .report-table-wrap { overflow: visible; border-color: #ccc; }
  .report-section.print-target table { min-width: 0; font-size: 9pt; }
  .report-section.print-target th, .report-section.print-target td { padding: 6px 7px; font-size: 9pt; }
  .report-section.print-target .report-section-heading { border-bottom: 1px solid #aaa; padding-bottom: 12px; }
  .daily-row { break-inside: avoid; }
}
</style>
