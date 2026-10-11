<script setup lang="ts">
import type { Patient } from '#shared/types/patient'
import type { Medication } from '#shared/types/medication'
import type { ClinicalNote, Treatment, TreatmentInput } from '#shared/types/treatment'

const props = defineProps<{ treatmentId?: number }>()
const editing = computed(() => props.treatmentId !== undefined)
useHead({ title: editing.value ? 'د تداوۍ سمون — روغتیا' : 'نوې تداوي — روغتیا' })
const patientsApi = usePatients()
const medicationsApi = useMedications()
const treatmentsApi = useTreatments()
const auth = useAuth()
const toast = useToast()
const patientSearch = ref('')
const patientOptions = ref<Patient[]>([])
const selectedPatient = ref<Patient | null>(null)
const patientLoading = ref(false)
const medications = ref<Medication[]>([])
const notes = ref<ClinicalNote[]>([])
const selectedNoteIds = ref<number[]>([])
const bloodPressure = ref('')
const rows = ref<{ key: number, medicationId: number | null, duration: string, refills: number | string }[]>([{ key: 1, medicationId: null, duration: '', refills: 0 }])
const noteOpen = ref(false)
const noteText = ref('')
const noteSaving = ref(false)
const pending = ref(false)
const loading = ref(true)
const ready = ref(false)
const errorMessage = ref('')
const serviceMessage = ref('')
let rowKey = 1
let patientRequest = 0
let patientTimer: ReturnType<typeof setTimeout> | undefined
const medicationOptions = computed(() => medications.value.map(medication => ({ id: medication.id, label: medication.name, type: medication.type })))
const medicationMap = computed(() => new Map(medications.value.map(medication => [medication.id, medication])))
const genders: Record<string, string> = { Male: 'نارینه', Female: 'ښځینه', Other: 'نور' }

async function loadExisting() {
  if (!props.treatmentId) return
  const treatment: Treatment = await treatmentsApi.get(props.treatmentId)
  selectedPatient.value = await patientsApi.get(treatment.patient.id)
  bloodPressure.value = treatment.bloodPressure
  selectedNoteIds.value = treatment.clinicalNotes.map(note => note.id)
  rows.value = treatment.prescriptionItems.map(item => ({ key: ++rowKey, medicationId: item.medicationId, duration: item.duration, refills: item.refills }))
}

async function loadPatients() {
  const current = ++patientRequest
  patientLoading.value = true
  try {
    const response = await patientsApi.list({ page: 1, pageSize: 20, search: patientSearch.value.trim() })
    if (current === patientRequest) patientOptions.value = response.items
  } catch { if (current === patientRequest) errorMessage.value = 'د ناروغانو لېست را نه وړل شو.' }
  finally { if (current === patientRequest) patientLoading.value = false }
}

async function loadOptions() {
  loading.value = true
  ready.value = false
  errorMessage.value = ''
  try {
    const [first, clinicalNotes] = await Promise.all([medicationsApi.list({ page: 1, pageSize: 100 }), treatmentsApi.clinicalNotes()])
    const all = [...first.items]
    const totalPages = Math.ceil(first.total / 100)
    for (let page = 2; page <= totalPages; page++) {
      const response = await medicationsApi.list({ page, pageSize: 100 })
      all.push(...response.items)
    }
    medications.value = all
    notes.value = clinicalNotes
    await loadPatients()
    await loadExisting()
    ready.value = true
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    errorMessage.value = 'اړین معلومات را نه وړل شول. مهرباني وکړئ بیا هڅه وکړئ.'
  } finally { loading.value = false }
}

function addRow() { if (rows.value.length < 30) rows.value.push({ key: ++rowKey, medicationId: null, duration: '', refills: 0 }) }
function removeRow(key: number) { rows.value = rows.value.filter(row => row.key !== key) }
function selectPatient(patient: Patient) { selectedPatient.value = patient; patientOptions.value = [] }

async function addNote() {
  const text = noteText.value.trim()
  if (!text || text.length > 300 || noteSaving.value) { serviceMessage.value = 'کلینیکي یادښت باید له 1 تر 300 تورو وي.'; return }
  noteSaving.value = true
  serviceMessage.value = ''
  try {
    const saved = await treatmentsApi.addClinicalNote(text)
    if (!notes.value.some(note => note.id === saved.id)) notes.value.push(saved)
    if (!selectedNoteIds.value.includes(saved.id)) selectedNoteIds.value.push(saved.id)
    noteText.value = ''
    noteOpen.value = false
    toast.add({ title: 'کلینیکي یادښت ثبت شو', color: 'success' })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    serviceMessage.value = 'یادښت خوندي نه شو. مهرباني وکړئ بیا هڅه وکړئ.'
  } finally { noteSaving.value = false }
}

async function save(printAfter = false) {
  if (pending.value) return
  serviceMessage.value = ''
  if (!selectedPatient.value) { serviceMessage.value = 'لومړی ناروغ وټاکئ.'; return }
  if (bloodPressure.value.trim() && !/^[1-9][0-9]{1,2}\/[1-9][0-9]{1,2}$/.test(bloodPressure.value.trim())) {
    serviceMessage.value = 'د وینې فشار د 120/80 په بڼه ولیکئ.'; return
  }
  if (rows.value.some(row => row.medicationId === null || !medicationMap.value.has(row.medicationId) ||
      !row.duration.trim() || row.duration.trim().length > 100 ||
      String(row.refills).trim() === '' || !Number.isInteger(Number(row.refills)) || Number(row.refills) < 0 || Number(row.refills) > 100)) {
    serviceMessage.value = 'د هر درمل نوم، موده او د تکرار شمېر وګورئ. که درمل نشته، تشه کرښه لرې کړئ.'; return
  }
  const input: TreatmentInput = {
    patientId: selectedPatient.value.id, bloodPressure: bloodPressure.value.trim(), clinicalNoteIds: selectedNoteIds.value,
    prescriptionItems: rows.value.map(row => ({ medicationId: row.medicationId!, duration: row.duration.trim(), refills: Number(row.refills) }))
  }
  pending.value = true
  try {
    const saved = editing.value ? await treatmentsApi.update(props.treatmentId!, input) : await treatmentsApi.create(input)
    toast.add({ title: editing.value ? 'تداوي تازه شوه' : 'تداوي ثبت شوه', description: 'معلومات او نسخه خوندي شول.', color: 'success', icon: 'i-lucide-circle-check' })
    await navigateTo(printAfter ? `/treatments/${saved.id}/print?auto=1` : '/treatments')
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { data?: { code?: string } } }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    serviceMessage.value = (failure.statusCode || failure.status) === 404 ? 'دا تداوي ونه موندل شوه. لېست تازه کړئ.' : failure.data?.data?.code === 'VALIDATION_ERROR'
      ? 'معلومات سم نه دي یا ټاکل شوی ناروغ، درمل یا یادښت نور شتون نه لري.'
      : 'تداوي خوندي نه شوه. مهرباني وکړئ بیا هڅه وکړئ.'
    toast.add({ title: 'تداوي خوندي نه شوه', description: serviceMessage.value, color: 'error' })
  } finally { pending.value = false }
}

watch(patientSearch, () => { clearTimeout(patientTimer); patientTimer = setTimeout(() => { void loadPatients() }, 350) })
watch(() => auth.user.value?.id, () => { selectedPatient.value = null; patientOptions.value = []; medications.value = []; notes.value = []; selectedNoteIds.value = []; rows.value = [{ key: ++rowKey, medicationId: null, duration: '', refills: 0 }]; void loadOptions() })
onBeforeUnmount(() => { ++patientRequest; clearTimeout(patientTimer) })
await loadOptions()
</script>

<template>
  <section class="new-treatment" aria-labelledby="new-treatment-title">
    <header class="page-heading"><div><div class="eyebrow"><span /> د ناروغ د لیدنې ثبت</div><h1 id="new-treatment-title">{{ editing ? 'د تداوۍ سمون' : 'نوې تداوي' }}</h1><p>ناروغ وټاکئ، د درملو نسخه او د تداوۍ معلومات ولیکئ.</p></div><UButton to="/treatments" color="neutral" variant="outline" icon="i-lucide-arrow-right" size="lg">د تداویو لېست</UButton></header>
    <div v-if="errorMessage" class="page-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ errorMessage }}</span><UButton color="neutral" variant="outline" :loading="loading" @click="loadOptions">بیا هڅه وکړئ</UButton></div>
    <div v-if="loading" class="page-loading" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" />معلومات را اخیستل کېږي…</div>
    <form v-else-if="ready" class="treatment-form" @submit.prevent="save(false)">
      <section class="panel form-section" aria-labelledby="patient-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-user-round" /></span><div><h2 id="patient-title">د ناروغ معلومات</h2><p>ناروغ د نوم یا کوډ له مخې پیدا او وټاکئ.</p></div></header>
        <div v-if="selectedPatient" class="selected-patient"><div><strong><bdi>{{ selectedPatient.name }}</bdi></strong><p>{{ selectedPatient.code }} · {{ localNumber(selectedPatient.age) }} کاله · {{ genders[selectedPatient.gender] || selectedPatient.gender }}</p><p v-if="selectedPatient.phone" dir="ltr">{{ selectedPatient.phone }}</p><p v-if="selectedPatient.address"><bdi>{{ selectedPatient.address }}</bdi></p></div><UButton type="button" color="neutral" variant="outline" icon="i-lucide-repeat-2" @click="selectedPatient = null; loadPatients()">بدلول</UButton></div>
        <div v-else class="patient-picker"><UInput v-model="patientSearch" icon="i-lucide-search" placeholder="د ناروغ نوم یا کوډ ولیکئ" aria-label="ناروغ ولټوئ" :maxlength="200" size="xl" class="w-full" /><div v-if="patientLoading" class="picker-state">ناروغان لټول کېږي…</div><div v-else-if="patientOptions.length" class="patient-results"><button v-for="patient in patientOptions" :key="patient.id" type="button" @click="selectPatient(patient)"><strong><bdi>{{ patient.name }}</bdi></strong><span>{{ patient.code }} · {{ localNumber(patient.age) }} کاله</span></button></div><p v-else class="picker-state">ناروغ ونه موندل شو. د ناروغانو په پاڼه کې یې ثبت کړئ.</p></div>
      </section>
      <section class="panel form-section" aria-labelledby="prescription-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-pill" /></span><div><h2 id="prescription-title">درمل او نسخه</h2><p>درمل وټاکئ؛ ډول یې په خپله ښودل کېږي.</p></div></header>
        <div v-if="!medications.length" class="picker-state">درمل نه دي ثبت شوي. <NuxtLink to="/medications">د درملو پاڼې ته لاړ شئ</NuxtLink>.</div>
        <div class="prescription-table-wrap"><table class="prescription-table"><thead><tr><th>شمېره</th><th>درمل</th><th>ډول</th><th>موده</th><th>تکرار</th><th><span class="sr-only">لرې کول</span></th></tr></thead><tbody><tr v-for="(row, index) in rows" :key="row.key"><td data-label="شمېره" class="row-number">{{ localNumber(index + 1) }}</td><td data-label="درمل"><USelectMenu v-model="row.medicationId" :items="medicationOptions" value-key="id" label-key="label" searchable searchable-placeholder="درمل ولټوئ" placeholder="درمل وټاکئ" size="lg" class="w-full" :aria-label="`د ${index + 1} کرښې درمل`" /></td><td data-label="ډول"><span class="type-value">{{ row.medicationId ? medicationMap.get(row.medicationId)?.type || '—' : '—' }}</span></td><td data-label="موده"><UInput v-model="row.duration" :maxlength="100" placeholder="لکه 7 ورځې" size="lg" class="w-full" :aria-label="`د ${index + 1} کرښې موده`" /></td><td data-label="تکرار"><UInput v-model="row.refills" type="number" inputmode="numeric" :min="0" :max="100" :step="1" size="lg" class="w-full" :aria-label="`د ${index + 1} کرښې تکرار`" /></td><td data-label="لرې کول"><UButton type="button" color="error" variant="ghost" icon="i-lucide-trash-2" :aria-label="`د ${index + 1} کرښه لرې کړئ`" @click="removeRow(row.key)" /></td></tr></tbody></table></div>
        <UButton type="button" color="neutral" variant="outline" icon="i-lucide-plus" :disabled="rows.length >= 30 || !medications.length" @click="addRow">بل درمل ورزیات کړئ</UButton>
      </section>
      <section class="panel form-section" aria-labelledby="treatment-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-heart-pulse" /></span><div><h2 id="treatment-title">د تداوۍ معلومات</h2><p>د وینې فشار او کلینیکي یادښتونه ثبت کړئ.</p></div></header>
        <div class="treatment-fields"><UFormField label="د وینې فشار (BP)" name="bloodPressure" help="لکه 120/80؛ اختیاري"><UInput v-model="bloodPressure" name="bloodPressure" dir="ltr" inputmode="numeric" :maxlength="16" placeholder="120/80" size="xl" class="w-full" /></UFormField><div class="notes-field"><UFormField label="کلینیکي یادښتونه" name="clinicalNotes"><USelectMenu v-model="selectedNoteIds" :items="notes" value-key="id" label-key="text" multiple searchable searchable-placeholder="یادښت ولټوئ" placeholder="یادښتونه وټاکئ" size="xl" class="w-full" /></UFormField><UButton type="button" color="neutral" variant="outline" icon="i-lucide-plus" @click="noteOpen = true">نوی یادښت</UButton></div></div>
      </section>
      <div v-if="serviceMessage" class="page-error" role="alert"><UIcon name="i-lucide-circle-alert" />{{ serviceMessage }}</div>
      <div class="form-actions"><UButton type="submit" icon="i-lucide-save" size="lg" :loading="pending" :disabled="pending">{{ editing ? 'بدلونونه خوندي کړئ' : 'تداوي خوندي کړئ' }}</UButton><UButton type="button" color="neutral" variant="outline" icon="i-lucide-printer" size="lg" :disabled="pending" @click="save(true)">خوندي او چاپ</UButton><UButton to="/treatments" color="neutral" variant="ghost" size="lg">بېرته</UButton></div>
    </form>
    <UModal v-model:open="noteOpen" title="نوی کلینیکي یادښت" description="دا یادښت وروسته هم د تداوۍ لپاره ټاکلی شئ." :dismissible="!noteSaving" :content="{ dir: 'rtl' }"><template #body><form class="note-form" @submit.prevent="addNote"><UFormField label="یادښت" required><UTextarea v-model="noteText" :maxlength="300" :rows="3" class="w-full" placeholder="کلینیکي یادښت ولیکئ" /></UFormField><p v-if="serviceMessage" role="alert">{{ serviceMessage }}</p><div><UButton type="submit" :loading="noteSaving">خوندي کول</UButton><UButton type="button" color="neutral" variant="ghost" :disabled="noteSaving" @click="noteOpen = false">بېرته</UButton></div></form></template></UModal>
  </section>
</template>

<style scoped>
.new-treatment { width: 100%; max-width: 1250px; min-width: 0; margin-inline: auto; }
.page-heading p { font-size: 1rem; line-height: 1.8; }
.treatment-form { display: grid; gap: 22px; }
.form-section { padding: 24px; border-radius: 16px; min-width: 0; }
.section-heading { display: flex; align-items: center; gap: 14px; margin-bottom: 22px; }
.section-icon { display: grid; place-items: center; width: 46px; height: 46px; flex-shrink: 0; color: var(--teal); background: var(--soft); border-radius: 13px; }
.section-icon .iconify { width: 24px; height: 24px; }
.section-heading h2 { margin: 0; font-size: 1.2rem; }
.section-heading p { margin: 3px 0 0; color: var(--muted); }
.patient-picker { display: grid; gap: 10px; max-width: 640px; }
.patient-results { display: grid; max-height: 270px; overflow-y: auto; border: 1px solid var(--line); border-radius: 12px; }
.patient-results button { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 5px 12px; padding: 12px 16px; border-bottom: 1px solid var(--line); text-align: start; }
.patient-results button:last-child { border-bottom: 0; }
.patient-results button:hover, .patient-results button:focus-visible { background: var(--soft); }
.patient-results span, .picker-state { color: var(--muted); }
.selected-patient { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 16px; padding: 18px; border: 1px solid var(--line); border-radius: 13px; }
.selected-patient strong { font-size: 1.125rem; }
.selected-patient p { margin: 5px 0 0; color: var(--muted); }
.prescription-table-wrap { width: 100%; overflow-x: auto; margin-bottom: 15px; }
.prescription-table { width: 100%; min-width: 750px; border-collapse: collapse; }
.prescription-table th, .prescription-table td { padding: 10px; border-bottom: 1px solid var(--line); text-align: start; }
.prescription-table th { color: var(--muted); font-size: .875rem; font-weight: 500; }
.prescription-table th:nth-child(1) { width: 7%; } .prescription-table th:nth-child(2) { width: 30%; } .prescription-table th:nth-child(3) { width: 19%; } .prescription-table th:nth-child(4) { width: 24%; } .prescription-table th:nth-child(5) { width: 12%; }
.row-number { font-weight: 650; }.type-value { overflow-wrap: anywhere; }
.treatment-fields { display: grid; grid-template-columns: minmax(0, 260px) minmax(0, 1fr); gap: 20px; align-items: end; }
.notes-field { display: flex; gap: 10px; align-items: end; min-width: 0; }
.notes-field > :first-child { min-width: 0; flex: 1; }
.notes-field > button { flex-shrink: 0; min-height: 44px; }
.page-error { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; padding: 16px; margin-bottom: 18px; border-radius: 11px; background: var(--soft); color: var(--teal); }
.page-loading { display: flex; align-items: center; gap: 10px; padding: 30px; }.page-loading .iconify { width: 24px; height: 24px; }
.form-actions { display: flex; flex-wrap: wrap; gap: 10px; }.form-actions > button, .form-actions > a { min-height: 46px; }
.note-form { display: grid; gap: 16px; }.note-form > div:last-child { display: flex; gap: 10px; }
@media (max-width: 760px) { .treatment-fields { grid-template-columns: minmax(0, 1fr); } .notes-field { flex-wrap: wrap; }.notes-field > :first-child { flex-basis: 100%; } .prescription-table { display: block; min-width: 0; } .prescription-table thead { display: none; } .prescription-table tbody { display: grid; gap: 12px; } .prescription-table tr { display: grid; gap: 10px; padding: 14px; border: 1px solid var(--line); border-radius: 12px; } .prescription-table td { display: grid; grid-template-columns: minmax(0, 1fr); gap: 5px; min-width: 0; padding: 0; border: 0; } .prescription-table td::before { content: attr(data-label); color: var(--muted); font-size: .875rem; } .prescription-table td:last-child { justify-items: start; } }
@media (max-width: 600px) { .form-section { padding: 18px 16px; }.page-heading > a { width: 100%; justify-content: center; }.patient-results button { display: grid; }.form-actions > button { flex: 1; justify-content: center; } }
</style>
