<script setup lang="ts">
import type { Treatment, TreatmentPage, TreatmentQuery, TreatmentSummary } from '#shared/types/treatment'

const props = defineProps<{ prescriptionsOnly?: boolean }>()
const api = useTreatments()
const auth = useAuth()
const toast = useToast()
const pageSize = 10
const mode = ref<'today' | 'date' | 'day' | 'week' | 'month'>('today')
const selectedDate = ref(jalaliToday())
const patientName = ref('')
const appliedName = ref('')
const page = ref(1)
const result = ref<TreatmentPage>({ items: [], total: 0, page: 1, pageSize })
const loading = ref(true)
const loaded = ref(false)
const errorMessage = ref('')
const detail = ref<Treatment | null>(null)
const detailOpen = ref(false)
const detailLoadingId = ref<number | null>(null)
const totalPages = computed(() => Math.max(1, Math.ceil(result.value.total / pageSize)))
let sequence = 0
let searchTimer: ReturnType<typeof setTimeout> | undefined

function dateRange(): Pick<TreatmentQuery, 'fromUtc' | 'toUtc'> {
  const date = mode.value === 'today' ? jalaliToday() : selectedDate.value
  let start = date
  if (mode.value === 'week') {
    const weekday = new Date(date.toDate('Asia/Kabul').getTime() + 270 * 60000).getUTCDay()
    start = date.subtract({ days: (weekday + 1) % 7 })
  } else if (mode.value === 'month') start = date.set({ day: 1 })
  const end = mode.value === 'month' ? start.add({ months: 1 }) : start.add({ days: mode.value === 'week' ? 7 : 1 })
  return { fromUtc: start.toDate('Asia/Kabul').toISOString(), toUtc: end.toDate('Asia/Kabul').toISOString() }
}

async function load() {
  const current = ++sequence
  const userId = auth.user.value?.id
  loading.value = true
  errorMessage.value = ''
  try {
    const response = await api.list({ page: page.value, pageSize, patientName: appliedName.value, ...dateRange(), hasPrescription: props.prescriptionsOnly === true })
    if (current !== sequence || userId !== auth.user.value?.id) return
    result.value = response
    loaded.value = true
    if (page.value > totalPages.value) { page.value = totalPages.value; await load() }
  } catch (error: unknown) {
    if (current !== sequence || userId !== auth.user.value?.id) return
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) {
      toast.add({ title: 'Sign in again', description: 'Your session has expired.', color: 'warning' })
      await navigateTo('/login')
      return
    }
    errorMessage.value = 'Unable to load treatments. Please try again.'
  } finally { if (current === sequence) loading.value = false }
}

function changeMode(value: typeof mode.value) { mode.value = value; page.value = 1; void load() }
function changePage(value: number) { if (value < 1 || value > totalPages.value || loading.value) return; page.value = value; void load() }
function applyName() { clearTimeout(searchTimer); appliedName.value = patientName.value.trim(); page.value = 1; void load() }
function moveDay(days: number) { selectedDate.value = selectedDate.value.add({ days }) }
async function showDetail(item: TreatmentSummary) {
  if (detailLoadingId.value !== null) return
  detailLoadingId.value = item.id
  try { detail.value = await api.get(item.id); detailOpen.value = true }
  catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) await navigateTo('/login')
    else toast.add({ title: 'Unable to load details', description: 'Please try again.', color: 'error' })
  } finally { detailLoadingId.value = null }
}

watch(selectedDate, () => { if (mode.value !== 'today') { page.value = 1; void load() } })
watch(patientName, () => { clearTimeout(searchTimer); if (patientName.value.trim() !== appliedName.value) searchTimer = setTimeout(applyName, 350) })
watch(() => auth.user.value?.id, () => { ++sequence; result.value = { items: [], total: 0, page: 1, pageSize }; detail.value = null; detailOpen.value = false; void load() })
onBeforeUnmount(() => { ++sequence; clearTimeout(searchTimer) })
await load()
</script>

<template>
  <section class="treatment-list" :aria-label="prescriptionsOnly ? 'Prescriptions' : 'Treatments'">
    <header class="page-heading">
      <div><div class="eyebrow"><span /> {{ prescriptionsOnly ? 'Saved prescriptions' : 'Patient care' }}</div><h1>{{ prescriptionsOnly ? 'Prescriptions' : 'Treatments' }}</h1><p>{{ prescriptionsOnly ? 'View saved prescriptions and their medications.' : 'View patient treatments and record a new treatment.' }}</p></div>
      <UButton to="/treatments/new" icon="i-lucide-plus" size="lg" class="new-button">New treatment</UButton>
    </header>
    <section class="panel list-panel" aria-label="Treatment list" :aria-busy="loading">
      <div class="filter-area">
        <div class="filter-heading"><span class="filter-icon"><UIcon :name="prescriptionsOnly ? 'i-lucide-clipboard-plus' : 'i-lucide-stethoscope'" /></span><div><h2>{{ prescriptionsOnly ? 'Prescription list' : 'Treatment list' }}</h2><p aria-live="polite">{{ loaded ? `${localNumber(result.total)} records` : 'Loading records' }}</p></div></div>
        <div class="range-buttons" role="group" aria-label="Date filter">
          <UButton v-for="option in ([['today', 'Today'], ['date', 'Specific date'], ['day', 'Day'], ['week', 'Week'], ['month', 'Month']] as const)" :key="option[0]" type="button" size="sm" :color="mode === option[0] ? 'primary' : 'neutral'" :variant="mode === option[0] ? 'solid' : 'ghost'" :aria-pressed="mode === option[0]" @click="changeMode(option[0])">{{ option[1] }}</UButton>
        </div>
        <div class="filter-controls">
          <form class="name-search" role="search" @submit.prevent="applyName"><UInput v-model="patientName" icon="i-lucide-search" placeholder="Search patient name" aria-label="Patient name" :maxlength="200" size="lg" class="w-full" /><UButton type="submit" color="neutral" variant="outline" size="lg">Search</UButton></form>
          <div v-if="mode !== 'today'" class="date-controls"><UButton v-if="mode === 'day'" type="button" icon="i-lucide-chevron-left" color="neutral" variant="outline" aria-label="Previous day" @click="moveDay(-1)" /><JalaliPicker v-model="selectedDate" /><UButton v-if="mode === 'day'" type="button" icon="i-lucide-chevron-right" color="neutral" variant="outline" aria-label="Next day" @click="moveDay(1)" /></div>
          <span v-else class="today-label">{{ formatJalali(jalaliToday()) }}</span>
        </div>
      </div>
      <div v-if="errorMessage" class="list-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ errorMessage }}</span><UButton color="neutral" variant="outline" @click="load">Try again</UButton></div>
      <div v-if="loading" class="list-state" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" /><p>Loading information…</p></div>
      <template v-else-if="loaded && result.items.length">
        <div class="table-wrap"><table><caption class="sr-only">Recorded treatments</caption><thead><tr><th>Patient</th><th>Code</th><th>Date</th><th>Blood pressure</th><th>Medications</th><th>Notes</th><th><span class="sr-only">Actions</span></th></tr></thead><tbody><tr v-for="item in result.items" :key="item.id"><td><strong><bdi>{{ item.patient.name }}</bdi></strong></td><td>{{ item.patient.code }}</td><td>{{ formatJalaliDateTime(item.recordedAtUtc) }}</td><td dir="ltr">{{ item.bloodPressure || '—' }}</td><td>{{ localNumber(item.medicationCount) }}</td><td>{{ localNumber(item.clinicalNoteCount) }}</td><td><div class="row-actions"><UButton type="button" color="neutral" variant="ghost" icon="i-lucide-eye" :loading="detailLoadingId === item.id" :disabled="detailLoadingId !== null" @click="showDetail(item)">Details</UButton><UButton :to="`/treatments/${item.id}/edit`" color="neutral" variant="ghost" icon="i-lucide-pencil-line">Edit</UButton><UButton :to="`/treatments/${item.id}/print`" color="neutral" variant="ghost" icon="i-lucide-printer">Print</UButton></div></td></tr></tbody></table></div>
        <div class="mobile-list"><article v-for="item in result.items" :key="item.id" class="treatment-card"><header><div><h3><bdi>{{ item.patient.name }}</bdi></h3><p>{{ item.patient.code }} · {{ formatJalaliDateTime(item.recordedAtUtc) }}</p></div><span class="medicine-count">{{ localNumber(item.medicationCount) }} Medications</span></header><div class="card-meta"><span>BP: <bdi dir="ltr">{{ item.bloodPressure || '—' }}</bdi></span><span>{{ localNumber(item.clinicalNoteCount) }} Notes</span></div><div class="card-actions"><UButton type="button" color="neutral" variant="outline" icon="i-lucide-eye" :loading="detailLoadingId === item.id" :disabled="detailLoadingId !== null" @click="showDetail(item)">Details</UButton><UButton :to="`/treatments/${item.id}/edit`" color="neutral" variant="outline" icon="i-lucide-pencil-line">Edit</UButton><UButton :to="`/treatments/${item.id}/print`" color="neutral" variant="outline" icon="i-lucide-printer">Print</UButton></div></article></div>
        <footer class="list-footer"><span>{{ localNumber((page - 1) * pageSize + 1) }}–{{ localNumber((page - 1) * pageSize + result.items.length) }} of {{ localNumber(result.total) }}</span><nav aria-label="pages"><UButton icon="i-lucide-chevron-left" color="neutral" variant="outline" :disabled="page <= 1 || loading" aria-label="Previous page" @click="changePage(page - 1)" /><span>{{ localNumber(page) }} / {{ localNumber(totalPages) }}</span><UButton icon="i-lucide-chevron-right" color="neutral" variant="outline" :disabled="page >= totalPages || loading" aria-label="Next page" @click="changePage(page + 1)" /></nav></footer>
      </template>
      <div v-else-if="loaded && !errorMessage" class="list-state"><span class="empty-icon"><UIcon name="i-lucide-clipboard-list" /></span><h3>{{ appliedName ? 'Patient not found' : 'No treatments in this period' }}</h3><p>{{ appliedName ? 'Try another name or change the date range.' : 'Record a new treatment or choose another date.' }}</p><UButton v-if="!prescriptionsOnly" to="/treatments/new" icon="i-lucide-plus">New treatment</UButton></div>
    </section>
    <TreatmentDetail v-if="detail" v-model:open="detailOpen" :treatment="detail" />
  </section>
</template>

<style scoped>
.treatment-list { width: 100%; max-width: 1250px; min-width: 0; margin-inline: auto; }
.page-heading p { font-size: 1rem; line-height: 1.8; }
.new-button { min-height: 46px; font-size: 1rem; flex-shrink: 0; }
.list-panel { min-width: 0; border-radius: 16px; overflow: hidden; }
.filter-area { display: grid; gap: 18px; padding: 24px; border-bottom: 1px solid var(--line); }
.filter-heading { display: flex; align-items: center; gap: 12px; }
.filter-heading h2 { margin: 0; font-size: 1.125rem; }
.filter-heading p { margin: 2px 0 0; color: var(--muted); font-size: .875rem; }
.filter-icon, .empty-icon { display: grid; place-items: center; width: 46px; height: 46px; border-radius: 13px; color: var(--teal); background: var(--soft); }
.filter-icon .iconify { width: 24px; height: 24px; }
.range-buttons { display: flex; flex-wrap: wrap; gap: 6px; }
.range-buttons button { min-height: 40px; font-size: .95rem; }
.filter-controls { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 14px; min-width: 0; }
.name-search { display: flex; gap: 8px; width: min(100%, 420px); min-width: 0; }
.name-search > :first-child { min-width: 0; flex: 1; }
.date-controls { display: flex; align-items: center; gap: 6px; min-width: 0; }
.today-label { color: var(--muted); }
.list-error { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 20px 24px; color: var(--teal); }
.list-state { display: flex; align-items: center; justify-content: center; flex-direction: column; gap: 12px; min-height: 280px; padding: 30px; text-align: center; }
.list-state > .iconify { width: 28px; height: 28px; color: var(--teal); }
.list-state h3, .list-state p { margin: 0; }
.list-state p { color: var(--muted); }
.table-wrap { width: 100%; overflow-x: auto; }
table { width: 100%; min-width: 970px; border-collapse: collapse; text-align: start; }
.row-actions, .card-actions { display: flex; flex-wrap: wrap; gap: 4px; }
.row-actions { min-width: 210px; }
.card-actions > * { flex: 1; justify-content: center; }
th, td { padding: 16px 14px; border-bottom: 1px solid var(--line); text-align: start; }
thead { background: var(--canvas); color: var(--muted); font-size: .875rem; }
tbody tr:hover { background: var(--canvas); }
tbody tr:last-child > * { border-bottom: 0; }
.mobile-list { display: none; }
.list-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 14px; padding: 18px 24px; border-top: 1px solid var(--line); color: var(--muted); }
.list-footer nav { display: flex; align-items: center; gap: 12px; }
@media (max-width: 900px) { .table-wrap { display: none; } .mobile-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; padding: 18px; } .treatment-card { min-width: 0; border: 1px solid var(--line); border-radius: 13px; padding: 16px; } .treatment-card header { display: flex; justify-content: space-between; gap: 10px; flex-wrap: wrap; } .treatment-card h3 { margin: 0; font-size: 1.1rem; } .treatment-card p { margin: 4px 0 0; color: var(--muted); font-size: .875rem; } .medicine-count { align-self: flex-start; background: var(--soft); color: var(--teal); border-radius: 8px; padding: 4px 8px; } .card-meta { display: flex; flex-wrap: wrap; gap: 14px; margin: 16px 0; color: var(--muted); } }
@media (max-width: 640px) { .filter-area { padding: 18px 16px; } .mobile-list { grid-template-columns: minmax(0, 1fr); padding: 16px; } .list-footer { padding: 16px; } .new-button { width: 100%; justify-content: center; } .name-search { width: 100%; } }
@media (max-width: 360px) { .name-search { flex-wrap: wrap; } .name-search > :first-child { flex-basis: 100%; } }
</style>
