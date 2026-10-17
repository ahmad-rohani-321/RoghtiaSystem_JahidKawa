<script setup lang="ts">
import type { Patient, PatientInput, PatientPage } from '#shared/types/patient'
import type { Medication } from '#shared/types/medication'
import { prescriptionFrequencies, type AdditionalInstruction, type ClinicalNote, type Treatment, type TreatmentInput } from '#shared/types/treatment'

const props = defineProps<{ treatmentId?: number }>()
const editing = computed(() => props.treatmentId !== undefined)
useHead({ title: editing.value ? 'Edit treatment — E-Roghtia' : 'New treatment — E-Roghtia' })
const patientsApi = usePatients()
const medicationsApi = useMedications()
const treatmentsApi = useTreatments()
const auth = useAuth()
const toast = useToast()
const selectedPatient = ref<Patient | null>(null)
const patientCode = ref('')
const patientDraft = reactive({ name: '', phone: '', age: '' as number | string, gender: '', address: '' })
const patientCodeMessage = ref('')
const patientCodeStatus = ref<'idle' | 'found' | 'missing' | 'error'>('idle')
const patientLookupPending = ref(false)
const patientModalOpen = ref(false)
const patientFilters = reactive({ name: '', phone: '', gender: 'all' })
const patientResults = ref<PatientPage>({ items: [], total: 0, page: 1, pageSize: 10 })
const patientSearchPage = ref(1)
const patientSearchLoading = ref(false)
const patientSearchError = ref('')
const medications = ref<Medication[]>([])
const notes = ref<ClinicalNote[]>([])
const selectedNoteIds = ref<number[]>([])
const instructionOptions = ref<AdditionalInstruction[]>([])
const selectedInstructionIds = ref<number[]>([])
const bloodPressure = ref('120/80')
const legacyAdditionalInstructions = ref('')
const rows = ref<{ key: number, medicationId: number | null, dose: number | string, frequency: string, duration: string }[]>([{ key: 1, medicationId: null, dose: 1, frequency: 'Once daily', duration: '' }])
const noteOpen = ref(false)
const noteText = ref('')
const noteSaving = ref(false)
const instructionOpen = ref(false)
const instructionText = ref('')
const instructionSaving = ref(false)
const pending = ref(false)
const loading = ref(true)
const ready = ref(false)
const errorMessage = ref('')
const serviceMessage = ref('')
const insufficientStockOpen = ref(false)
const insufficientStockPrint = ref(false)
let rowKey = 1
let patientRequest = 0
let patientLookupRequest = 0
let patientLookupTask: Promise<boolean> | null = null
const medicationOptions = computed(() => medications.value.map(medication => ({ id: medication.id, label: `${medication.name} · ${localNumber(medication.quantity)}` })))
const medicationMap = computed(() => new Map(medications.value.map(medication => [medication.id, medication])))
const frequencyOptions = prescriptionFrequencies.map(value => ({ label: value, value }))
const genders: Record<string, string> = { Male: 'Male', Female: 'Female', Other: 'Other' }
const genderOptions = [{ label: 'All genders', value: 'all' }, { label: 'Male', value: 'Male' }, { label: 'Female', value: 'Female' }, { label: 'Other', value: 'Other' }]
const patientTotalPages = computed(() => Math.max(1, Math.ceil(patientResults.value.total / 10)))

function populatePatient(patient: Patient, cancelLookup = true) {
  if (cancelLookup) {
    ++patientLookupRequest
    patientLookupPending.value = false
    patientLookupTask = null
  }
  selectedPatient.value = patient
  patientCode.value = patient.code
  Object.assign(patientDraft, { name: patient.name, phone: patient.phone, age: patient.age, gender: patient.gender, address: patient.address })
  patientCodeMessage.value = ''
  patientCodeStatus.value = 'found'
  serviceMessage.value = ''
  patientModalOpen.value = false
}

function clearPatient() {
  ++patientLookupRequest
  patientLookupPending.value = false
  patientLookupTask = null
  selectedPatient.value = null
  patientCode.value = ''
  Object.assign(patientDraft, { name: '', phone: '', age: '', gender: '', address: '' })
  patientCodeMessage.value = ''
  patientCodeStatus.value = 'idle'
}

function onPatientCodeChange(value: string | number | null | undefined) {
  const next = String(value ?? '')
  if (selectedPatient.value && next.trim().toUpperCase() !== selectedPatient.value.code) {
    selectedPatient.value = null
    Object.assign(patientDraft, { name: '', phone: '', age: '', gender: '', address: '' })
  }
  ++patientLookupRequest
  patientLookupPending.value = false
  patientLookupTask = null
  patientCodeMessage.value = ''
  patientCodeStatus.value = 'idle'
}

function lookupPatientCode(): Promise<boolean> {
  if (patientLookupPending.value && patientLookupTask) return patientLookupTask
  const task = runPatientCodeLookup()
  patientLookupTask = task
  void task.finally(() => { if (patientLookupTask === task) patientLookupTask = null })
  return task
}

async function runPatientCodeLookup(): Promise<boolean> {
  const code = patientCode.value.trim().toUpperCase()
  if (!code) { patientCodeMessage.value = ''; patientCodeStatus.value = 'idle'; return false }
  if (selectedPatient.value?.code === code) return true
  const request = ++patientLookupRequest
  patientCode.value = code
  selectedPatient.value = null
  if (!/^RT-[1-9][0-9]*$/.test(code) || code.length > 32) {
    patientCodeMessage.value = 'Enter a code such as RT-1 or leave it blank to generate a new one.'
    patientCodeStatus.value = 'error'
    return false
  }
  patientLookupPending.value = true
  patientCodeMessage.value = ''
  patientCodeStatus.value = 'idle'
  try {
    const patient = await patientsApi.getByCode(code)
    if (request !== patientLookupRequest || code !== patientCode.value) return false
    populatePatient(patient, false)
    return true
  } catch (error: unknown) {
    if (request !== patientLookupRequest || code !== patientCode.value) return false
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) { await navigateTo('/login'); return false }
    patientCodeMessage.value = status === 404
      ? 'Code not found. Enter the new patient details; a new code will be generated.'
      : 'Unable to look up patient code. Please try again.'
    patientCodeStatus.value = status === 404 ? 'missing' : 'error'
    return false
  } finally { if (request === patientLookupRequest) patientLookupPending.value = false }
}

async function searchPatients() {
  const request = ++patientRequest
  patientSearchLoading.value = true
  patientSearchError.value = ''
  try {
    const result = await patientsApi.list({ page: patientSearchPage.value, pageSize: 10,
      name: patientFilters.name.trim(), phone: patientFilters.phone.trim(), gender: patientFilters.gender === 'all' ? '' : patientFilters.gender })
    if (request === patientRequest) patientResults.value = result
  } catch (error: unknown) {
    if (request !== patientRequest) return
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    patientSearchError.value = 'Unable to load patients. Please try again.'
  } finally { if (request === patientRequest) patientSearchLoading.value = false }
}

function openPatientSearch() {
  patientSearchPage.value = 1
  patientModalOpen.value = true
  void searchPatients()
}

function changePatientPage(page: number) {
  if (page < 1 || page > patientTotalPages.value || patientSearchLoading.value) return
  patientSearchPage.value = page
  void searchPatients()
}

function validatePatientDraft(): PatientInput | null {
  if (!patientDraft.name.trim() || patientDraft.name.trim().length > 200 ||
      patientDraft.phone.length > 32 || patientDraft.address.length > 1000 ||
      !['Male', 'Female', 'Other'].includes(patientDraft.gender) ||
      String(patientDraft.age).trim() === '' || !Number.isInteger(Number(patientDraft.age)) ||
      Number(patientDraft.age) < 0 || Number(patientDraft.age) > 150) {
    serviceMessage.value = 'Check the patient name, age, gender, phone and address.'
    return null
  }
  return { name: patientDraft.name.trim(), phone: patientDraft.phone.trim(), age: Number(patientDraft.age),
    gender: patientDraft.gender, address: patientDraft.address.trim() }
}

async function createPatient() {
  if (pending.value || patientLookupPending.value || selectedPatient.value) return
  serviceMessage.value = ''
  if (patientCode.value.trim()) {
    const checkedCode = patientCode.value.trim().toUpperCase()
    await lookupPatientCode()
    if (selectedPatient.value || patientCode.value !== checkedCode || patientCodeStatus.value !== 'missing') return
  }
  const input = validatePatientDraft()
  if (!input) return
  pending.value = true
  try {
    const created = await patientsApi.create(input)
    populatePatient(created)
    toast.add({ title: 'New patient registered', description: `Code: ${created.code}`, color: 'success' })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    serviceMessage.value = 'Unable to register patient. Check the information and try again.'
    toast.add({ title: 'Patient registration failed', description: serviceMessage.value, color: 'error' })
  } finally { pending.value = false }
}

async function loadExisting() {
  if (!props.treatmentId) return
  const treatment: Treatment = await treatmentsApi.get(props.treatmentId)
  populatePatient(await patientsApi.get(treatment.patient.id))
  bloodPressure.value = treatment.bloodPressure
  legacyAdditionalInstructions.value = treatment.additionalInstructions
  selectedNoteIds.value = treatment.clinicalNotes.map(note => note.id)
  selectedInstructionIds.value = treatment.additionalInstructionItems.map(item => item.id)
  rows.value = treatment.prescriptionItems.map(item => ({ key: ++rowKey, medicationId: item.medicationId, dose: item.dose, frequency: item.frequency, duration: item.duration }))
}

async function loadOptions() {
  loading.value = true
  ready.value = false
  errorMessage.value = ''
  try {
    const [first, clinicalNotes, savedInstructions] = await Promise.all([medicationsApi.list({ page: 1, pageSize: 100 }), treatmentsApi.clinicalNotes(), treatmentsApi.additionalInstructions()])
    const all = [...first.items]
    const totalPages = Math.ceil(first.total / 100)
    for (let page = 2; page <= totalPages; page++) {
      const response = await medicationsApi.list({ page, pageSize: 100 })
      all.push(...response.items)
    }
    medications.value = all
    notes.value = clinicalNotes
    instructionOptions.value = savedInstructions
    await loadExisting()
    ready.value = true
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    errorMessage.value = 'Unable to load required information. Please try again.'
  } finally { loading.value = false }
}

function addRow() { if (rows.value.length < 30) rows.value.push({ key: ++rowKey, medicationId: null, dose: 1, frequency: 'Once daily', duration: '' }) }
function removeRow(key: number) { rows.value = rows.value.filter(row => row.key !== key) }
async function continueWithoutStockDeduction() {
  insufficientStockOpen.value = false
  await save(insufficientStockPrint.value, true)
}
async function addNote() {
  const text = noteText.value.trim()
  if (!text || text.length > 300 || noteSaving.value) { serviceMessage.value = 'Clinical note must contain 1 to 300 characters.'; return }
  noteSaving.value = true
  serviceMessage.value = ''
  try {
    const saved = await treatmentsApi.addClinicalNote(text)
    if (!notes.value.some(note => note.id === saved.id)) notes.value.push(saved)
    if (!selectedNoteIds.value.includes(saved.id)) selectedNoteIds.value.push(saved.id)
    noteText.value = ''
    noteOpen.value = false
    toast.add({ title: 'Clinical note added', color: 'success' })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    serviceMessage.value = 'Unable to save note. Please try again.'
  } finally { noteSaving.value = false }
}

async function addInstruction() {
  const text = instructionText.value.trim()
  if (!text || text.length > 300 || instructionSaving.value) { serviceMessage.value = 'Additional instruction must contain 1 to 300 characters.'; return }
  instructionSaving.value = true
  serviceMessage.value = ''
  try {
    const saved = await treatmentsApi.addAdditionalInstruction(text)
    if (!instructionOptions.value.some(item => item.id === saved.id)) instructionOptions.value.push(saved)
    if (!selectedInstructionIds.value.includes(saved.id)) selectedInstructionIds.value.push(saved.id)
    instructionText.value = ''
    instructionOpen.value = false
    toast.add({ title: 'Additional instruction added', color: 'success' })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    serviceMessage.value = 'Unable to save instruction. Please try again.'
  } finally { instructionSaving.value = false }
}

async function save(printAfter = false, allowInsufficientStock = false) {
  if (pending.value) return
  serviceMessage.value = ''
  if (patientLookupPending.value && patientLookupTask) await patientLookupTask
  if (patientLookupPending.value) { serviceMessage.value = 'Please wait for the patient code lookup to finish.'; return }
  if (patientCode.value.trim() && selectedPatient.value?.code !== patientCode.value.trim().toUpperCase()) {
    const checkedCode = patientCode.value.trim().toUpperCase()
    if (patientCodeStatus.value === 'idle') await lookupPatientCode()
    if (patientLookupPending.value || patientCode.value !== checkedCode) return
    if (patientCodeStatus.value === 'error' || (patientCode.value && !selectedPatient.value && patientCodeStatus.value !== 'missing')) {
      serviceMessage.value = patientCodeMessage.value; return
    }
  }
  const newPatient = selectedPatient.value ? null : validatePatientDraft()
  if (!selectedPatient.value && !newPatient) return
  if (bloodPressure.value.trim() && !/^[1-9][0-9]{1,2}\/[1-9][0-9]{1,2}$/.test(bloodPressure.value.trim())) {
    serviceMessage.value = 'Enter blood pressure in the format 120/80.'; return
  }
  if (rows.value.some(row => row.medicationId === null || !medicationMap.value.has(row.medicationId) ||
      String(row.dose).trim() === '' || !Number.isInteger(Number(row.dose)) || Number(row.dose) < 1 || Number(row.dose) > 2147483647 ||
      !frequencyOptions.some(option => option.value === row.frequency) ||
      !row.duration.trim() || row.duration.trim().length > 100)) {
    serviceMessage.value = 'Check the medication, dose, frequency and duration in each row. Remove empty rows.'; return
  }
  pending.value = true
  try {
    if (!selectedPatient.value && newPatient) {
      const created = await patientsApi.create(newPatient)
      populatePatient(created)
      toast.add({ title: 'New patient registered', description: `Code: ${created.code}`, color: 'success' })
    }
    const input: TreatmentInput = {
      patientId: selectedPatient.value!.id, bloodPressure: bloodPressure.value.trim(), additionalInstructions: legacyAdditionalInstructions.value, clinicalNoteIds: selectedNoteIds.value, additionalInstructionIds: selectedInstructionIds.value,
      prescriptionItems: rows.value.map(row => ({ medicationId: row.medicationId!, dose: Number(row.dose), frequency: row.frequency, duration: row.duration.trim() })),
      allowInsufficientStock
    }
    const saved = editing.value ? await treatmentsApi.update(props.treatmentId!, input) : await treatmentsApi.create(input)
    toast.add({ title: editing.value ? 'Treatment updated' : 'Treatment recorded', description: 'Treatment and prescription saved.', color: 'success', icon: 'i-lucide-circle-check' })
    await navigateTo(printAfter ? `/treatments/${saved.id}/print?auto=1` : '/treatments')
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number, data?: { code?: string, data?: { code?: string } } }
    if ((failure.statusCode || failure.status) === 401) return await navigateTo('/login')
    const code = failure.data?.data?.code || failure.data?.code
    if ((failure.statusCode || failure.status) === 409 && code === 'INSUFFICIENT_STOCK') {
      insufficientStockPrint.value = printAfter
      insufficientStockOpen.value = true
      return
    }
    serviceMessage.value = (failure.statusCode || failure.status) === 404 ? 'Patient or treatment not found. Refresh the list.' : code === 'VALIDATION_ERROR'
      ? 'Invalid information, or the selected patient, medication or note is no longer available.'
      : 'Unable to save treatment. Please try again.'
    toast.add({ title: 'Treatment could not be saved', description: serviceMessage.value, color: 'error' })
  } finally { pending.value = false }
}

watch(() => auth.user.value?.id, () => { clearPatient(); ++patientRequest; patientModalOpen.value = false; insufficientStockOpen.value = false; patientResults.value = { items: [], total: 0, page: 1, pageSize: 10 }; medications.value = []; notes.value = []; selectedNoteIds.value = []; instructionOptions.value = []; selectedInstructionIds.value = []; bloodPressure.value = '120/80'; legacyAdditionalInstructions.value = ''; rows.value = [{ key: ++rowKey, medicationId: null, dose: 1, frequency: 'Once daily', duration: '' }]; void loadOptions() })
onBeforeUnmount(() => { ++patientRequest; ++patientLookupRequest })
await loadOptions()
</script>

<template>
  <section class="new-treatment" aria-labelledby="new-treatment-title">
    <header class="page-heading"><div><div class="eyebrow"><span /> Record a patient visit</div><h1 id="new-treatment-title">{{ editing ? 'Edit treatment' : 'New treatment' }}</h1><p>Select or register a patient, then enter the prescription and treatment details.</p></div><UButton to="/treatments" color="neutral" variant="outline" icon="i-lucide-arrow-left" size="lg">Treatment list</UButton></header>
    <div v-if="errorMessage" class="page-error" role="alert"><UIcon name="i-lucide-circle-alert" /><span>{{ errorMessage }}</span><UButton color="neutral" variant="outline" :loading="loading" @click="loadOptions">Try again</UButton></div>
    <div v-if="loading" class="page-loading" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" />Loading information…</div>
    <form v-else-if="ready" class="treatment-form" @submit.prevent="save(false)">
      <section class="panel form-section" aria-labelledby="patient-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-user-round" /></span><div><h2 id="patient-title">Patient information</h2><p>Enter a code and leave the field, or use search to select a patient.</p></div></header>
        <div class="patient-toolbar">
          <p v-if="selectedPatient" class="selected-hint"><UIcon name="i-lucide-circle-check" /> Selected patient · ID {{ localNumber(selectedPatient.id) }}</p>
          <p v-else class="selected-hint">A new patient code is generated when you save.</p>
          <div class="patient-toolbar-actions"><UButton type="button" color="neutral" variant="outline" icon="i-lucide-search" :disabled="pending" @click="openPatientSearch">Find patient</UButton><UButton v-if="selectedPatient" type="button" color="neutral" variant="ghost" icon="i-lucide-user-plus" :disabled="pending" @click="clearPatient">New patient</UButton><UButton v-else type="button" color="neutral" variant="outline" icon="i-lucide-user-plus" :loading="pending" :disabled="pending || patientLookupPending" @click="createPatient">Register patient</UButton></div>
        </div>
        <div v-if="selectedPatient" class="patient-fields selected-patient-details" aria-label="Selected patient information">
          <div><span>Patient code</span><bdi dir="ltr">{{ selectedPatient.code }}</bdi></div>
          <div><span>Patient name</span><bdi>{{ selectedPatient.name }}</bdi></div>
          <div><span>Phone</span><bdi dir="ltr">{{ selectedPatient.phone || '—' }}</bdi></div>
          <div><span>Age (years)</span><bdi>{{ localNumber(selectedPatient.age) }}</bdi></div>
          <div><span>Gender</span><bdi>{{ genders[selectedPatient.gender] || selectedPatient.gender }}</bdi></div>
          <div class="patient-address"><span>Address</span><bdi>{{ selectedPatient.address || '—' }}</bdi></div>
        </div>
        <div v-else class="patient-fields">
          <UFormField label="Patient code" name="patientCode" help="For example RT-1; leave blank for a new patient"><UInput v-model="patientCode" name="patientCode" dir="ltr" autocomplete="off" :maxlength="32" placeholder="RT-1" size="xl" class="w-full" :loading="patientLookupPending" @update:model-value="onPatientCodeChange" @blur="lookupPatientCode" /></UFormField>
          <UFormField label="Patient name" name="patientName" required><UInput v-model="patientDraft.name" name="patientName" dir="auto" :maxlength="200" size="xl" class="w-full" /></UFormField>
          <UFormField label="Phone" name="patientPhone" hint="Optional"><UInput v-model="patientDraft.phone" name="patientPhone" type="tel" dir="ltr" :maxlength="32" size="xl" class="w-full" /></UFormField>
          <UFormField label="Age (years)" name="patientAge" required><UInput v-model="patientDraft.age" name="patientAge" type="number" inputmode="numeric" :min="0" :max="150" :step="1" size="xl" class="w-full" /></UFormField>
          <UFormField label="Gender" name="patientGender" required><USelect v-model="patientDraft.gender" name="patientGender" :items="genderOptions.slice(1)" placeholder="Select gender" size="xl" class="w-full" /></UFormField>
          <UFormField label="Address" name="patientAddress" hint="Optional" class="patient-address"><UTextarea v-model="patientDraft.address" name="patientAddress" dir="auto" :maxlength="1000" :rows="2" size="xl" class="w-full" /></UFormField>
        </div>
        <p v-if="patientCodeMessage" class="patient-code-message" role="status">{{ patientCodeMessage }}</p>
      </section>
      <section class="panel form-section" aria-labelledby="prescription-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-pill" /></span><div><h2 id="prescription-title">Medications and prescription</h2><p>Select a medication; its type is filled automatically.</p></div></header>
        <div v-if="!medications.length" class="picker-state">No medications registered. <NuxtLink to="/medications">Open medications</NuxtLink>.</div>
        <div class="prescription-table-wrap"><table class="prescription-table"><thead><tr><th>No.</th><th>Medications</th><th>Type</th><th>Dose</th><th>Frequency</th><th>Duration</th><th><span class="sr-only">Remove</span></th></tr></thead><tbody><tr v-for="(row, index) in rows" :key="row.key"><td data-label="No." class="row-number">{{ localNumber(index + 1) }}</td><td data-label="Medications"><USelectMenu v-model="row.medicationId" :items="medicationOptions" value-key="id" label-key="label" searchable searchable-placeholder="Search medications" placeholder="Select medication" size="lg" class="w-full" :aria-label="`Row ${index + 1} medication`" /></td><td data-label="Type"><span class="type-value">{{ row.medicationId ? medicationMap.get(row.medicationId)?.type || '—' : '—' }}</span></td><td data-label="Dose"><UInput v-model="row.dose" type="number" inputmode="numeric" :min="1" :max="2147483647" :step="1" size="lg" class="w-full" :aria-label="`Row ${index + 1} dose`" /></td><td data-label="Frequency"><USelect v-model="row.frequency" :items="frequencyOptions" size="lg" class="w-full" :aria-label="`Row ${index + 1} frequency`" /></td><td data-label="Duration"><UInput v-model="row.duration" :maxlength="100" placeholder="e.g. 7 days" size="lg" class="w-full" :aria-label="`Row ${index + 1} duration`" /></td><td data-label="Remove"><UButton type="button" color="error" variant="ghost" icon="i-lucide-trash-2" :aria-label="`Row ${index + 1} remove row`" @click="removeRow(row.key)" /></td></tr></tbody></table></div>
        <UButton type="button" color="neutral" variant="outline" icon="i-lucide-plus" :disabled="rows.length >= 30 || !medications.length" @click="addRow">Add medication row</UButton>
      </section>
      <section class="panel form-section" aria-labelledby="treatment-title"><header class="section-heading"><span class="section-icon"><UIcon name="i-lucide-heart-pulse" /></span><div><h2 id="treatment-title">Treatment information</h2><p>Record blood pressure and clinical notes.</p></div></header>
        <div class="treatment-fields"><UFormField label="Blood pressure (BP)" name="bloodPressure" help="e.g. 120/80"><UInput v-model="bloodPressure" name="bloodPressure" dir="ltr" inputmode="numeric" :maxlength="16" placeholder="120/80" size="xl" class="w-full" /></UFormField><div class="notes-field"><UFormField label="Clinical notes" name="clinicalNotes"><USelectMenu v-model="selectedNoteIds" :items="notes" value-key="id" label-key="text" multiple searchable searchable-placeholder="Search notes" placeholder="Select notes" size="xl" class="w-full" /></UFormField><UButton type="button" color="neutral" variant="outline" icon="i-lucide-plus" @click="noteOpen = true">New note</UButton></div><div class="notes-field instructions-field"><UFormField label="Additional instructions" name="additionalInstructions"><USelectMenu v-model="selectedInstructionIds" :items="instructionOptions" value-key="id" label-key="text" multiple searchable searchable-placeholder="Search instructions" placeholder="Select instructions" size="xl" class="w-full" /></UFormField><UButton type="button" color="neutral" variant="outline" icon="i-lucide-plus" @click="instructionOpen = true">Add instruction</UButton></div><p class="instructions-help">Select one or more saved instructions to print with the prescription.</p></div>
      </section>
      <div v-if="serviceMessage" class="page-error" role="alert"><UIcon name="i-lucide-circle-alert" />{{ serviceMessage }}</div>
      <div class="form-actions"><UButton type="submit" icon="i-lucide-save" size="lg" :loading="pending" :disabled="pending">{{ editing ? 'Save changes' : 'Save treatment' }}</UButton><UButton type="button" color="neutral" variant="outline" icon="i-lucide-printer" size="lg" :disabled="pending" @click="save(true)">Save and print</UButton><UButton to="/treatments" color="neutral" variant="ghost" size="lg">Back</UButton></div>
    </form>
    <UModal v-model:open="patientModalOpen" title="Find patient" description="Search by name, phone or gender, then select a patient." :content="{ dir: 'ltr' }" :ui="{ content: 'w-[min(940px,calc(100vw-1.5rem))] max-w-none min-w-0', body: 'min-w-0' }">
      <template #body>
        <div class="patient-search-modal">
          <div class="patient-search-filters"><UFormField label="Name"><UInput v-model="patientFilters.name" :maxlength="200" placeholder="Name" size="lg" class="w-full" @keyup.enter="patientSearchPage = 1; searchPatients()" /></UFormField><UFormField label="Phone"><UInput v-model="patientFilters.phone" type="tel" dir="ltr" :maxlength="32" placeholder="Phone" size="lg" class="w-full" @keyup.enter="patientSearchPage = 1; searchPatients()" /></UFormField><UFormField label="Gender"><USelect v-model="patientFilters.gender" :items="genderOptions" size="lg" class="w-full" /></UFormField><UButton type="button" icon="i-lucide-search" :loading="patientSearchLoading" @click="patientSearchPage = 1; searchPatients()">Search</UButton></div>
          <p v-if="patientSearchError" role="alert" class="patient-code-message">{{ patientSearchError }}</p>
          <div v-else-if="patientSearchLoading" class="picker-state" role="status">Loading patients…</div>
          <div v-else-if="patientResults.items.length" class="patient-modal-table-wrap"><table class="patient-modal-table"><thead><tr><th scope="col">Code</th><th scope="col">Name</th><th scope="col">Phone</th><th scope="col">Age</th><th scope="col">Gender</th><th scope="col">Address</th><th scope="col"><span class="sr-only">Select</span></th></tr></thead><tbody><tr v-for="patient in patientResults.items" :key="patient.id"><td><bdi>{{ patient.code }}</bdi></td><th scope="row"><bdi>{{ patient.name }}</bdi></th><td><bdi dir="ltr">{{ patient.phone || '—' }}</bdi></td><td>{{ localNumber(patient.age) }}</td><td>{{ genders[patient.gender] || patient.gender }}</td><td><bdi>{{ patient.address || '—' }}</bdi></td><td><UButton type="button" size="sm" :aria-label="`${patient.name} Select`" @click="populatePatient(patient)">Select</UButton></td></tr></tbody></table></div>
          <p v-else class="picker-state">No patient found. Change the search criteria or register a new patient.</p>
          <footer v-if="patientResults.total > 10 && !patientSearchError" class="patient-modal-pagination"><span>{{ localNumber(patientResults.total) }} patients · {{ localNumber(patientSearchPage) }} / {{ localNumber(patientTotalPages) }}</span><div><UButton type="button" color="neutral" variant="outline" icon="i-lucide-chevron-left" aria-label="Previous page" :disabled="patientSearchPage <= 1 || patientSearchLoading" @click="changePatientPage(patientSearchPage - 1)" /><UButton type="button" color="neutral" variant="outline" icon="i-lucide-chevron-right" aria-label="Next page" :disabled="patientSearchPage >= patientTotalPages || patientSearchLoading" @click="changePatientPage(patientSearchPage + 1)" /></div></footer>
        </div>
      </template>
    </UModal>
    <UModal v-model:open="insufficientStockOpen" title="Insufficient medication stock" description="The dose exceeds the available quantity for one or more medications." :dismissible="!pending" :content="{ dir: 'ltr' }">
      <template #body><div class="stock-warning"><p>If you select «Yes» , the treatment will be saved without deducting medications with insufficient stock. If you select «No» , the treatment will not be saved.</p><div><UButton type="button" :loading="pending" @click="continueWithoutStockDeduction">Yes, continue</UButton><UButton type="button" color="neutral" variant="outline" :disabled="pending" @click="insufficientStockOpen = false">No</UButton></div></div></template>
    </UModal>
    <UModal v-model:open="noteOpen" title="New clinical note" description="This note will also be available for future treatments." :dismissible="!noteSaving" :content="{ dir: 'ltr' }"><template #body><form class="note-form" @submit.prevent="addNote"><UFormField label="Remarks" required><UTextarea v-model="noteText" :maxlength="300" :rows="3" class="w-full" placeholder="Enter a clinical note" /></UFormField><p v-if="serviceMessage" role="alert">{{ serviceMessage }}</p><div><UButton type="submit" :loading="noteSaving">Save</UButton><UButton type="button" color="neutral" variant="ghost" :disabled="noteSaving" @click="noteOpen = false">Back</UButton></div></form></template></UModal>
    <UModal v-model:open="instructionOpen" title="New additional instruction" description="This instruction will be available for future treatments." :dismissible="!instructionSaving" :content="{ dir: 'ltr' }"><template #body><form class="note-form" @submit.prevent="addInstruction"><UFormField label="Instruction" required><UTextarea v-model="instructionText" :maxlength="300" :rows="3" class="w-full" placeholder="Enter an instruction for the patient" /></UFormField><p v-if="serviceMessage" role="alert">{{ serviceMessage }}</p><div><UButton type="submit" :loading="instructionSaving">Save instruction</UButton><UButton type="button" color="neutral" variant="ghost" :disabled="instructionSaving" @click="instructionOpen = false">Back</UButton></div></form></template></UModal>
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
.patient-toolbar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 18px; }
.patient-toolbar-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.selected-hint { display: flex; align-items: center; gap: 7px; margin: 0; color: var(--muted); line-height: 1.7; }
.selected-hint .iconify { color: var(--teal); }
.patient-fields { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px 20px; }
.patient-fields > * { min-width: 0; }
.patient-address { grid-column: 1 / -1; }
.selected-patient-details > div { display: grid; gap: 5px; min-width: 0; padding: 12px 14px; border: 1px solid var(--line); border-radius: 10px; background: var(--canvas); }
.selected-patient-details span { color: var(--muted); font-size: .875rem; }
.selected-patient-details bdi { min-width: 0; overflow-wrap: anywhere; font-weight: 600; }
.patient-code-message { margin: 14px 0 0; padding: 10px 12px; border-radius: 9px; background: var(--soft); color: var(--teal); line-height: 1.8; }
.patient-search-modal { display: grid; gap: 18px; min-width: 0; }
.patient-search-filters { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)) auto; gap: 12px; align-items: end; }
.patient-search-filters > * { min-width: 0; }
.patient-search-filters > button { min-height: 40px; }
.patient-modal-table-wrap { max-width: 100%; max-height: min(50vh, 480px); overflow: auto; border: 1px solid var(--line); border-radius: 12px; }
.patient-modal-table { width: 100%; min-width: 690px; border-collapse: collapse; }
.patient-modal-table th, .patient-modal-table td { padding: 11px 12px; border-bottom: 1px solid var(--line); text-align: start; overflow-wrap: anywhere; }
.patient-modal-table thead { background: var(--canvas); color: var(--muted); }
.patient-modal-table tbody tr:hover { background: var(--soft); }
.patient-modal-table tbody tr:last-child > * { border-bottom: 0; }
.patient-modal-table th { font-weight: 600; }
.patient-modal-pagination { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 10px; color: var(--muted); }
.patient-modal-pagination > div { display: flex; gap: 8px; }
.picker-state { color: var(--muted); padding: 14px 0; }
.prescription-table-wrap { width: 100%; overflow-x: auto; margin-bottom: 15px; }
.prescription-table { width: 100%; min-width: 880px; border-collapse: collapse; }
.prescription-table th, .prescription-table td { padding: 10px; border-bottom: 1px solid var(--line); text-align: start; }
.prescription-table th { color: var(--muted); font-size: .875rem; font-weight: 500; }
.prescription-table th:nth-child(1) { width: 6%; } .prescription-table th:nth-child(2) { width: 27%; } .prescription-table th:nth-child(3) { width: 14%; } .prescription-table th:nth-child(4) { width: 11%; } .prescription-table th:nth-child(5) { width: 21%; } .prescription-table th:nth-child(6) { width: 11%; }
.row-number { font-weight: 650; }.type-value { overflow-wrap: anywhere; }
.treatment-fields { display: grid; grid-template-columns: minmax(0, 260px) minmax(0, 1fr); gap: 20px; align-items: end; }
.instructions-field { grid-column: 1 / -1; }
.instructions-help { grid-column: 1 / -1; margin-top: -14px; color: var(--muted); font-size: .875rem; }
.notes-field { display: flex; gap: 10px; align-items: end; min-width: 0; }
.notes-field > :first-child { min-width: 0; flex: 1; }
.notes-field > button { flex-shrink: 0; min-height: 44px; }
.page-error { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; padding: 16px; margin-bottom: 18px; border-radius: 11px; background: var(--soft); color: var(--teal); }
.page-loading { display: flex; align-items: center; gap: 10px; padding: 30px; }.page-loading .iconify { width: 24px; height: 24px; }
.form-actions { display: flex; flex-wrap: wrap; gap: 10px; }.form-actions > button, .form-actions > a { min-height: 46px; }
.note-form { display: grid; gap: 16px; }.note-form > div:last-child { display: flex; gap: 10px; }
.stock-warning { display: grid; gap: 18px; }
.stock-warning p { margin: 0; color: var(--muted); line-height: 1.9; }
.stock-warning > div { display: flex; flex-wrap: wrap; gap: 10px; }
@media (max-width: 760px) { .patient-fields { grid-template-columns: repeat(2, minmax(0, 1fr)); } .patient-search-filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } .treatment-fields { grid-template-columns: minmax(0, 1fr); } .notes-field { flex-wrap: wrap; }.notes-field > :first-child { flex-basis: 100%; } .prescription-table { display: block; min-width: 0; } .prescription-table thead { display: none; } .prescription-table tbody { display: grid; gap: 12px; } .prescription-table tr { display: grid; gap: 10px; padding: 14px; border: 1px solid var(--line); border-radius: 12px; } .prescription-table td { display: grid; grid-template-columns: minmax(0, 1fr); gap: 5px; min-width: 0; padding: 0; border: 0; } .prescription-table td::before { content: attr(data-label); color: var(--muted); font-size: .875rem; } .prescription-table td:last-child { justify-items: start; } }
@media (max-width: 600px) { .form-section { padding: 18px 16px; }.page-heading > a { width: 100%; justify-content: center; }.patient-fields, .patient-search-filters { grid-template-columns: minmax(0, 1fr); }.patient-toolbar-actions, .patient-toolbar-actions > button, .patient-search-filters > button { width: 100%; justify-content: center; }.form-actions > button { flex: 1; justify-content: center; } }
</style>
