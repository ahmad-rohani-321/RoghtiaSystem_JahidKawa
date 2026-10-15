<script setup lang="ts">
import type { TreatmentSummary } from '#shared/types/treatment'

const { user } = useAuth()
const { dashboard: loadDashboard } = useTreatments()
const selectedDate = shallowRef(jalaliToday())
const period = ref('Week')
const search = ref('')
const customize = ref(false)
const detail = ref<TreatmentSummary | null>(null)
const toast = useToast()
const loading = ref(true)
const refreshing = ref(false)
const loadError = ref(false)
const summary = ref<Awaited<ReturnType<typeof loadDashboard>> | null>(null)
const lastUpdated = ref<Date | null>(null)
const chartIndex = ref(6)
let refreshTimer: ReturnType<typeof setInterval> | undefined
let refreshRequested = false
const widgets = ref([{ id: 'visits', label: 'Patient visits', visible: true }, { id: 'distribution', label: 'Patient distribution', visible: true }, { id: 'patients', label: 'Recent patients', visible: true }, { id: 'activity', label: 'Recent activity', visible: true }])

async function refreshDashboard(showError = false) {
  if (refreshing.value) { refreshRequested = true; return }
  refreshing.value = true
  try {
    const dateParts = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Kabul', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(selectedDate.value.toDate('Asia/Kabul'))
    const date = `${dateParts.find(part => part.type === 'year')?.value}-${dateParts.find(part => part.type === 'month')?.value}-${dateParts.find(part => part.type === 'day')?.value}`
    summary.value = await loadDashboard(date, period.value)
    lastUpdated.value = new Date()
    loadError.value = false
  } catch {
    loadError.value = true
    if (showError) toast.add({ title: 'Dashboard could not be refreshed', description: 'Check your connection and try again.', color: 'error', icon: 'i-lucide-circle-alert' })
  } finally {
    refreshing.value = false
    loading.value = false
    if (refreshRequested) { refreshRequested = false; refreshDashboard() }
  }
}

function onVisibilityChange() {
  if (document.visibilityState === 'visible') refreshDashboard()
}
function onDataChanged() { refreshDashboard() }

onMounted(() => {
  try {
    const saved = JSON.parse(localStorage.getItem('roghtia-widgets') || 'null')
    if (Array.isArray(saved) && saved.length === 4 && new Set(saved.map(w => w.id)).size === 4 && saved.every(w => widgets.value.some(x => x.id === w.id) && typeof w.visible === 'boolean')) widgets.value = saved.map(w => ({ ...widgets.value.find(x => x.id === w.id)!, visible: w.visible }))
  } catch {}
  refreshDashboard()
  refreshTimer = setInterval(() => refreshDashboard(), 30_000)
  document.addEventListener('visibilitychange', onVisibilityChange)
  window.addEventListener('focus', onVisibilityChange)
  window.addEventListener('roghtia:data-changed', onDataChanged)
})
onBeforeUnmount(() => {
  if (refreshTimer) clearInterval(refreshTimer)
  document.removeEventListener('visibilitychange', onVisibilityChange)
  window.removeEventListener('focus', onVisibilityChange)
  window.removeEventListener('roghtia:data-changed', onDataChanged)
})
watch(() => `${selectedDate.value.toString()}-${period.value}`, () => { if (import.meta.client) refreshDashboard() })
watch(widgets, value => { if (import.meta.client) localStorage.setItem('roghtia-widgets', JSON.stringify(value)) }, { deep: true })

function move(index: number, delta: number) { const next = index + delta; if (next < 0 || next >= widgets.value.length) return; const list = [...widgets.value]; [list[index], list[next]] = [list[next]!, list[index]!]; widgets.value = list }
const stats = computed(() => [
  { label: 'Total patients', value: summary.value?.totalPatients ?? 0, icon: 'i-lucide-users-round', tone: 'teal', unit: 'patients' },
  { label: 'Patients seen today', value: summary.value?.patientsToday ?? 0, icon: 'i-lucide-stethoscope', tone: 'blue', unit: 'patients' },
  { label: 'Prescriptions today', value: summary.value?.prescriptionsToday ?? 0, icon: 'i-lucide-clipboard-plus', tone: 'purple', unit: 'prescriptions' },
  { label: 'Medication records', value: summary.value?.totalMedications ?? 0, icon: 'i-lucide-capsule', tone: 'orange', unit: 'medications' }
])
const chartRows = computed(() => {
  const rows = summary.value?.visits || []
  if (period.value === 'Month') return Array.from({ length: Math.ceil(rows.length / 7) }, (_, index) => {
    const week = rows.slice(index * 7, (index + 1) * 7)
    return { date: week[0]?.date || '', visits: week.reduce((sum, row) => sum + row.visits, 0), prescriptions: week.reduce((sum, row) => sum + row.prescriptions, 0) }
  })
  return rows
})
const days = computed(() => chartRows.value.map(row => {
  const date = new Date(`${row.date}T12:00:00+04:30`)
  return new Intl.DateTimeFormat('en-US', { weekday: 'long', timeZone: 'Asia/Kabul' }).format(date)
}))
const chartValues = computed(() => chartRows.value.map(row => row.visits))
const prescriptionValues = computed(() => chartRows.value.map(row => row.prescriptions))
const totalVisits = computed(() => chartValues.value.reduce((sum, value) => sum + value, 0))
const peakValue = computed(() => Math.max(1, ...chartValues.value))
const chartPath = computed(() => {
  if (!chartValues.value.length) return ''
  const values = chartValues.value
  const points = values.map((value, index) => ({ x: values.length === 1 ? 350 : index * 700 / (values.length - 1), y: 150 - value / peakValue.value * 125 }))
  return points.map((point, index) => `${index ? 'L' : 'M'}${point.x} ${point.y}`).join(' ')
})
const chartAreaPath = computed(() => chartPath.value ? `${chartPath.value} L700 170 L0 170Z` : '')
const distribution = computed(() => {
  const rows = chartRows.value
  const patients = rows.reduce((sum, row) => sum + row.visits, 0)
  const prescriptions = rows.reduce((sum, row) => sum + row.prescriptions, 0)
  return [
    { label: 'Visits with prescription', count: prescriptions, percent: patients ? Math.round(prescriptions / patients * 100) : 0, tone: 'light' },
    { label: 'Visits without prescription', count: Math.max(0, patients - prescriptions), percent: patients ? Math.round(Math.max(0, patients - prescriptions) / patients * 100) : 0, tone: 'pale' }
  ]
})
const recentPatients = computed(() => {
  const query = search.value.trim().toLocaleLowerCase()
  const seen = new Set<number>()
  return (summary.value?.recentTreatments || []).filter(treatment => {
    if (seen.has(treatment.patient.id) || (query && !`${treatment.patient.name} ${treatment.patient.code} ${treatment.patient.phone}`.toLocaleLowerCase().includes(query))) return false
    seen.add(treatment.patient.id)
    return true
  }).slice(0, 6)
})
const recentActivity = computed(() => (summary.value?.recentTreatments || []).slice(0, 4).map(treatment => ({
  id: treatment.id,
  title: treatment.medicationCount ? 'Prescription recorded' : 'Patient visit recorded',
  text: `${treatment.patient.name} · ${treatment.medicationCount} medication${treatment.medicationCount === 1 ? '' : 's'}`,
  icon: treatment.medicationCount ? 'i-lucide-clipboard-plus' : 'i-lucide-stethoscope',
  time: formatJalaliDateTime(treatment.recordedAtUtc)
})))
function patientTone(id: number) { return ['teal', 'purple', 'blue', 'orange'][id % 4] }
function initials(name: string) { return name.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toLocaleUpperCase() }
function escapeSvg(value: string | number) {
  return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&apos;')
}
function printReport() {
  if (summary.value) window.print()
}
function exportReport() {
  if (!summary.value) return
  const rows = [['Metric', 'Count'], ...stats.value.map(stat => [stat.label, stat.value]), ['Visits in chart period', totalVisits.value]]
  const blob = new Blob(['\uFEFF' + rows.map(row => row.map(value => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a'); a.href = url; a.download = `roghtia-dashboard-${new Date().toISOString().slice(0, 10)}.csv`; a.click(); URL.revokeObjectURL(url)
  toast.add({ title: 'Report downloaded', description: 'The report contains current clinic data.', icon: 'i-lucide-check' })
}
async function exportImage() {
  if (!summary.value) return
  const width = 1600
  const height = 1200
  const cardValues = stats.value.map((stat, index) => {
    const x = 80 + index * 365
    return `<rect x="${x}" y="190" width="335" height="130" rx="18" fill="#ffffff" stroke="#dce9e9"/><text x="${x + 24}" y="234" fill="#61777e" font-size="22">${escapeSvg(stat.label)}</text><text x="${x + 24}" y="290" fill="#1d454b" font-size="42" font-weight="700">${escapeSvg(stat.value)} <tspan fill="#82969a" font-size="19" font-weight="400">${escapeSvg(stat.unit)}</tspan></text>`
  }).join('')
  const chartPoints = chartValues.value.map((value, index) => {
    const x = chartValues.value.length === 1 ? 800 : 150 + index * 1300 / (chartValues.value.length - 1)
    const y = 590 - value / peakValue.value * 220
    return { x, y, value, date: chartRows.value[index]?.date || '' }
  })
  const chartPath = chartPoints.map((point, index) => `${index ? 'L' : 'M'}${point.x},${point.y}`).join(' ')
  const chartSvg = `<path d="${chartPath}" fill="none" stroke="#159f96" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>${chartPoints.map(point => `<circle cx="${point.x}" cy="${point.y}" r="8" fill="#ffffff" stroke="#159f96" stroke-width="5"/>`).join('')}`
  const rows = recentPatients.value.slice(0, 6).map((treatment, index) => {
    const y = 895 + index * 42
    return `<text x="105" y="${y}" fill="#263f47" font-size="20">${escapeSvg(treatment.patient.name)}</text><text x="620" y="${y}" fill="#536b72" font-size="19">${escapeSvg(treatment.patient.code)}</text><text x="900" y="${y}" fill="#536b72" font-size="19">${escapeSvg(formatJalaliDateTime(treatment.recordedAtUtc))}</text><text x="1430" y="${y}" fill="#536b72" font-size="19" text-anchor="end">${escapeSvg(treatment.medicationCount)}</text><line x1="100" y1="${y + 14}" x2="1500" y2="${y + 14}" stroke="#e8eeee"/>`
  }).join('')
  const reportDate = escapeSvg(formatJalali(selectedDate.value))
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><rect width="100%" height="100%" fill="#f5f8fa"/><text x="80" y="90" fill="#147e78" font-size="24" font-weight="700">ROGHTIA HEALTHCARE</text><text x="80" y="145" fill="#193c43" font-size="38" font-weight="700">Dashboard report</text><text x="1515" y="100" fill="#536b72" font-size="20" text-anchor="end">${escapeSvg(user.value?.userName || '')} · ${reportDate} · ${escapeSvg(period.value)}</text>${cardValues}<rect x="80" y="355" width="900" height="360" rx="18" fill="#ffffff" stroke="#dce9e9"/><text x="110" y="405" fill="#263f47" font-size="24" font-weight="700">Patient visits</text><text x="945" y="405" fill="#61777e" font-size="19" text-anchor="end">${escapeSvg(totalVisits.value)} visits</text><line x1="140" y1="630" x2="940" y2="630" stroke="#e5eeee"/><line x1="140" y1="540" x2="940" y2="540" stroke="#e5eeee"/><line x1="140" y1="450" x2="940" y2="450" stroke="#e5eeee"/>${chartSvg}${chartPoints.map(point => `<text x="${point.x}" y="675" fill="#71858b" font-size="16" text-anchor="middle">${escapeSvg(point.date.slice(5))}</text>`).join('')}<rect x="1005" y="355" width="515" height="360" rx="18" fill="#ffffff" stroke="#dce9e9"/><text x="1040" y="405" fill="#263f47" font-size="24" font-weight="700">Visit distribution</text>${distribution.value.map((entry, index) => { const y = 485 + index * 105; const barWidth = 420 * entry.percent / 100; return `<text x="1040" y="${y}" fill="#536b72" font-size="18">${escapeSvg(entry.label)}</text><text x="1480" y="${y}" fill="#263f47" font-size="18" text-anchor="end">${escapeSvg(entry.percent)}% · ${escapeSvg(entry.count)}</text><rect x="1040" y="${y + 18}" width="420" height="14" rx="7" fill="#e8f1f0"/><rect x="1040" y="${y + 18}" width="${barWidth}" height="14" rx="7" fill="${index ? '#99c9c3' : '#159f96'}"/>` }).join('')}<rect x="80" y="755" width="1440" height="385" rx="18" fill="#ffffff" stroke="#dce9e9"/><text x="110" y="805" fill="#263f47" font-size="24" font-weight="700">Recent patients</text><text x="105" y="850" fill="#82969a" font-size="17">Patient</text><text x="620" y="850" fill="#82969a" font-size="17">Code</text><text x="900" y="850" fill="#82969a" font-size="17">Last visit</text><text x="1430" y="850" fill="#82969a" font-size="17" text-anchor="end">Prescriptions</text>${rows || '<text x="105" y="900" fill="#82969a" font-size="20">No recent visits recorded.</text>'}<text x="80" y="1170" fill="#82969a" font-size="16">Generated ${escapeSvg(new Date().toLocaleString())} · Roghtia</text></svg>`
  const svgUrl = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml;charset=utf-8' }))
  try {
    const image = new Image()
    await new Promise<void>((resolve, reject) => { image.onload = () => resolve(); image.onerror = () => reject(new Error('Image rendering failed.')); image.src = svgUrl })
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Canvas is unavailable.')
    context.drawImage(image, 0, 0, width, height)
    const png = await new Promise<Blob>((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('PNG export failed.')), 'image/png'))
    const pngUrl = URL.createObjectURL(png)
    const link = document.createElement('a'); link.href = pngUrl; link.download = `roghtia-dashboard-${new Date().toISOString().slice(0, 10)}.png`; link.click()
    URL.revokeObjectURL(pngUrl)
    toast.add({ title: 'Image downloaded', description: 'The dashboard report was saved as a PNG image.', icon: 'i-lucide-image' })
  } catch {
    toast.add({ title: 'Image export failed', description: 'Please try again in a moment.', color: 'error', icon: 'i-lucide-circle-alert' })
  } finally {
    URL.revokeObjectURL(svgUrl)
  }
}
const exportItems = computed(() => [[
  { label: 'Download CSV', icon: 'i-lucide-file-spreadsheet', onSelect: exportReport },
  { label: 'Print / Save as PDF', icon: 'i-lucide-file-text', onSelect: printReport },
  { label: 'Download PNG image', icon: 'i-lucide-image', onSelect: () => exportImage() }
]])
</script>
<template>
  <div class="dashboard">
    <div class="page-heading"><div><div class="eyebrow"><span /> A clear view of your clinic</div><h1>Healthcare overview</h1><p>Welcome, {{ user?.userName }}! Your care activity at a glance.</p></div><div class="heading-actions"><UButton color="neutral" variant="outline" icon="i-lucide-sliders-horizontal" @click="customize = true">Customize dashboard</UButton><UButton color="neutral" variant="outline" icon="i-lucide-refresh-cw" :loading="refreshing" @click="refreshDashboard(true)">Refresh</UButton><UDropdownMenu :items="exportItems"><UButton icon="i-lucide-download" :disabled="!summary">Export</UButton></UDropdownMenu></div></div>
    <section class="welcome-banner"><div class="welcome-copy"><span class="banner-label"><span class="live-dot" /> Live clinic overview</span><h2>One step closer to a healthier life.</h2><p>Your dashboard uses the latest information saved for your clinic.</p><div class="banner-meta"><span><UIcon name="i-lucide-calendar-days" />{{ formatJalali(selectedDate, true) }}</span><span v-if="lastUpdated"><UIcon name="i-lucide-refresh-cw" />Updated {{ lastUpdated.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}</span></div></div><div class="medical-art" aria-hidden="true"><div class="orbit orbit-one" /><div class="orbit orbit-two" /><span class="art-plus plus-one">+</span><span class="art-plus plus-two">+</span><div class="art-heart"><UIcon name="i-lucide-heart-pulse" /></div><div class="art-float"><UIcon name="i-lucide-shield-check" /><span>Care and trust</span></div><span class="art-dot" /></div></section>
    <UAlert v-if="loadError" color="warning" variant="subtle" icon="i-lucide-circle-alert" title="Some dashboard data could not be refreshed." description="Showing the latest data available. Use Refresh to try again." class="dashboard-alert" />
    <div class="filter-row"><div class="section-label"><span class="section-mark" /><h2>Clinic overview</h2><span class="subtle">{{ period }} activity</span></div><div class="filters"><JalaliPicker v-model="selectedDate" /></div></div>
    <div class="stats-grid"><template v-if="loading"><div v-for="n in 4" :key="n" class="stat-card panel dashboard-skeleton" aria-hidden="true" /></template><StatCard v-else v-for="stat in stats" :key="stat.label" v-bind="stat" /></div>
    <div class="widget-grid">
      <template v-for="widget in widgets.filter(w => w.visible)" :key="widget.id">
        <WidgetPanel v-if="widget.id === 'visits'" class="visits-widget" title="Patient visits" subtitle="Visits recorded over the selected period">
          <template #action><div class="period-switch"><button v-for="p in ['Day', 'Week', 'Month']" :key="p" :class="{ selected: period === p }" @click="period = p">{{ p }}</button></div></template>
          <div class="chart-summary"><strong>{{ localNumber(totalVisits) }}<small>visits</small></strong><span class="chart-legend"><span><i />All visits</span><span><i class="legend-secondary" />With prescription</span></span></div>
          <div v-if="loading" class="chart-loading">Loading clinic activity…</div><div v-else-if="!chartRows.length" class="chart-loading">No visit data is available yet.</div><div v-else class="chart" role="group" aria-label="Patient visits chart"><div class="chart-y"><span v-for="n in [1, .75, .5, .25, 0]" :key="n">{{ localNumber(Math.round(peakValue * n)) }}</span></div><div class="chart-body"><div class="chart-lines"><i v-for="n in 5" :key="n" /></div><svg viewBox="0 0 700 170" preserveAspectRatio="none" aria-hidden="true"><defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#20b4aa" stop-opacity=".22" /><stop offset="100%" stop-color="#20b4aa" stop-opacity="0" /></linearGradient></defs><path :d="chartAreaPath" fill="url(#area)"/><path :d="chartPath" fill="none" stroke="#16a99e" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/><path v-if="chartRows.length > 1" :d="chartRows.map((row, i) => `${i ? 'L' : 'M'}${chartRows.length === 1 ? 350 : i * 700 / (chartRows.length - 1)} ${150 - row.prescriptions / peakValue * 125}`).join(' ')" fill="none" stroke="#99c9cd" stroke-width="2" stroke-dasharray="6 5"/></svg><div class="chart-hover"><button v-for="(day, i) in days" :key="chartRows[i]?.date" :aria-label="`${day}: ${localNumber(chartValues[i] || 0)} visits`" @mouseenter="chartIndex = i" @focus="chartIndex = i" @click="chartIndex = i"><span v-if="chartIndex === i" class="chart-tooltip">{{ localNumber(chartValues[i] || 0) }} visits<small>{{ formatJalaliDateTime(`${chartRows[i]?.date}T08:00:00Z`) }}</small></span></button></div><div class="chart-x"><span v-for="(day, i) in days" :key="day + i" :title="day"><span class="chart-day-full">{{ day.slice(0, 3) }}</span><span class="chart-day-short">{{ day.slice(0, 1) }}</span></span></div></div></div>
        </WidgetPanel>
        <WidgetPanel v-else-if="widget.id === 'distribution'" class="distribution-widget" title="Visit distribution" subtitle="Current chart period"><template #action><UIcon name="i-lucide-chart-pie" class="muted-icon" /></template><div class="donut" :style="{ '--distribution': `${distribution[1]?.percent || 0}%` }"><div><strong>{{ localNumber(totalVisits) }}</strong><span>All visits</span></div></div><div class="distribution-list"><div v-for="entry in distribution" :key="entry.label"><span><i class="legend-dot" :class="entry.tone" />{{ entry.label }}</span><b>{{ localNumber(entry.percent) }}% <small>{{ localNumber(entry.count) }}</small></b></div></div></WidgetPanel>
        <WidgetPanel v-else-if="widget.id === 'patients'" class="patients-widget" title="Recent patients" subtitle="Patients from your latest recorded visits"><template #action><UButton to="/patients" variant="link" trailing-icon="i-lucide-arrow-right">All patients</UButton></template><div class="table-search"><UInput v-model="search" icon="i-lucide-search" placeholder="Search patient name or ID..." aria-label="Search patients" class="w-full" /></div><div class="table-scroll"><table><thead><tr><th>Patient</th><th>Code</th><th>Last visit</th><th>Prescription</th><th><span class="sr-only">Details</span></th></tr></thead><tbody><tr v-for="treatment in recentPatients" :key="treatment.patient.id"><td><div class="patient-cell"><span class="patient-avatar" :class="patientTone(treatment.patient.id)">{{ initials(treatment.patient.name) }}</span><span><strong>{{ treatment.patient.name }}</strong><small>{{ localNumber(treatment.patient.age) }} years · {{ treatment.patient.gender }}</small></span></div></td><td>{{ treatment.patient.code }}</td><td class="time-cell">{{ formatJalaliDateTime(treatment.recordedAtUtc) }}</td><td>{{ localNumber(treatment.medicationCount) }} items</td><td><UButton icon="i-lucide-chevron-right" variant="ghost" color="neutral" :aria-label="`${treatment.patient.name} Details`" @click="detail = treatment" /></td></tr><tr v-if="!loading && !recentPatients.length"><td colspan="5" class="empty-table">{{ search ? 'No matching patients found.' : 'No patient visits have been recorded yet.' }}</td></tr></tbody></table></div><div class="table-footer"><span>{{ localNumber(recentPatients.length) }} recent patients</span><span><span class="live-dot" />{{ refreshing ? 'Updating…' : 'Live data' }}</span></div></WidgetPanel>
        <WidgetPanel v-else-if="widget.id === 'activity'" class="activity-widget" title="Recent activity" subtitle="Latest saved treatments"><template #action><span class="today-tag">{{ localNumber(summary?.treatmentsToday || 0) }} today</span></template><div v-if="loading" class="chart-loading">Loading recent activity…</div><div v-else-if="!recentActivity.length" class="chart-loading">No activity has been recorded yet.</div><div v-else class="timeline"><div v-for="(event, i) in recentActivity" :key="event.id" class="timeline-item"><span class="timeline-icon" :class="['teal', 'blue', 'purple', 'orange'][i % 4]"><UIcon :name="event.icon" /></span><div><h3>{{ event.title }}</h3><p>{{ event.text }}</p><small>{{ event.time }}</small></div></div></div><div class="care-note"><UIcon name="i-lucide-heart-handshake" /><div><strong>Better care starts with better organization.</strong><p>Roghtia, your everyday partner.</p></div></div></WidgetPanel>
      </template>
    </div>
    <footer class="page-footer"><span>Roghtia <span>·</span> Built for better care</span><span><UIcon name="i-lucide-shield-check" /> Your care is our priority</span></footer>
    <UModal v-model:open="customize" title="Customize dashboard" description="Show, hide or reorder widgets."><template #body><div v-for="(w, i) in widgets" :key="w.id" class="widget-option"><USwitch v-model="w.visible" :label="w.label" /><div><UButton icon="i-lucide-arrow-up" variant="ghost" :disabled="i === 0" aria-label="Move up" @click="move(i, -1)" /><UButton icon="i-lucide-arrow-down" variant="ghost" :disabled="i === widgets.length - 1" aria-label="Move down" @click="move(i, 1)" /></div></div><p class="saved-note">Changes are saved automatically on this device.</p></template></UModal>
    <UModal :open="!!detail" title="Patient visit summary" description="Details from the selected patient's latest visit." @update:open="detail = null"><template #body><div v-if="detail" class="patient-detail"><span class="patient-avatar" :class="patientTone(detail.patient.id)">{{ initials(detail.patient.name) }}</span><h2>{{ detail.patient.name }}</h2><p>{{ detail.patient.code }} · {{ localNumber(detail.patient.age) }} years · {{ detail.patient.gender }}</p><p>{{ localNumber(detail.medicationCount) }} prescribed medication items · {{ localNumber(detail.clinicalNoteCount) }} clinical notes</p><p>Blood pressure: <bdi dir="ltr">{{ detail.bloodPressure || '—' }}</bdi></p><p>{{ formatJalaliDateTime(detail.recordedAtUtc) }}</p></div></template></UModal>
  </div>
</template>
