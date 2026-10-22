<script setup lang="ts">
import type { Treatment } from '#shared/types/treatment'

definePageMeta({ layout: 'print' })
const route = useRoute()
const id = Number(route.params.id)
if (!Number.isInteger(id) || id < 1 || id > 2147483647) throw createError({ statusCode: 404 })
const api = useTreatments()
const doctor = useDoctorInformation()
const auth = useAuth()
const treatment = ref<Treatment | null>(null)
const loading = ref(true)
const loadError = ref('')
const requestPending = ref(false)
const printPending = ref(false)
const printAsA5 = ref(route.query.a5 === '1')
const printGrayscale = ref(route.query.grayscale === '1')

const info = computed(() => doctor.information.value)
const doctorName = computed(() => info.value?.doctorNameEnglish || auth.user.value?.userName || 'Doctor')
const profession = computed(() => info.value?.doctorProfessionEnglish || '')
const hospitalLogo = computed(() => info.value?.hospitalLogo || null)
const hospitalName = computed(() => info.value?.hospitalNamePashto?.trim() || '')
const hospitalAddress = computed(() => info.value?.hospitalAddress?.trim() || '')
const hospitalPhoneNumbers = computed(() => info.value?.hospitalPhoneNumbers?.trim() || '')
const doctorLogo = computed(() => info.value?.doctorLogo || null)
const genders: Record<string, string> = { Male: 'Male', Female: 'Female', Other: 'Other' }
const dateParts = computed(() => (treatment.value ? formatJalaliDateTime(treatment.value.recordedAtUtc) : '').split(', '))
const prescriptionNumber = computed(() => {
  if (!treatment.value) return ''
  const year = new Date(treatment.value.recordedAtUtc).getUTCFullYear()
  return `RX-${year}-${String(treatment.value.id).padStart(6, '0')}`
})
const clinicalNotesText = computed(() => treatment.value?.clinicalNotes
  .map(note => note.text.trim()).filter(Boolean).join(', ') || '')
const additionalInstructionsText = computed(() => {
  const items = treatment.value?.additionalInstructionItems.map(item => item.text.trim()).filter(Boolean) || []
  const legacy = treatment.value?.additionalInstructions?.trim().replace(/\s*[\r\n]+\s*/g, ', ')
  if (legacy) items.push(legacy)
  return items.join(', ')
})
async function printDocument() {
  if (!treatment.value || loading.value || loadError.value || printPending.value) return
  printPending.value = true
  const pageStyle = document.createElement('style')
  pageStyle.textContent = `@media print { @page { size: ${printAsA5.value ? 'A5' : 'A4'} portrait; margin: 0; } }`
  document.head.append(pageStyle)
  try {
    await nextTick()
    await document.fonts.ready
    await Promise.all(Array.from(document.querySelectorAll('.print-sheet img')).map(image => (image as HTMLImageElement).decode().catch(() => {})))
    await new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve())))
    window.print()
  } finally {
    pageStyle.remove()
    printPending.value = false
  }
}

async function loadTreatment() {
  if (requestPending.value) return
  requestPending.value = true
  loading.value = true
  loadError.value = ''
  try {
    treatment.value = await api.get(id)
    try { await doctor.load() } catch { /* The prescription remains printable without optional branding. */ }
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) {
      await navigateTo({ path: '/login', query: { redirect: route.fullPath } })
      return
    }
    loadError.value = status === 404
      ? 'This treatment was not found or is no longer available to your account.'
      : 'The treatment could not be loaded. Check that the server is running, then try again.'
  } finally {
    requestPending.value = false
    loading.value = false
  }

  if (route.query.auto === '1' && treatment.value && !loadError.value) {
    await printDocument()
  }
}

useHead({ title: `Prescription ${prescriptionNumber.value} — E-Roghtia` })

onMounted(loadTreatment)
</script>

<template>
  <div class="print-page" :class="{ 'print-page-a5': printAsA5 }">
    <div v-if="loading" class="print-state" role="status"><UIcon name="i-lucide-loader-circle" class="animate-spin" />Loading treatment…</div>
    <div v-else-if="loadError" class="print-state print-error" role="alert"><span>{{ loadError }}</span><UButton type="button" color="neutral" variant="outline" icon="i-lucide-refresh-cw" @click="loadTreatment">Try again</UButton><UButton to="/treatments" color="neutral" variant="ghost">Back to treatments</UButton></div>
    <template v-else-if="treatment">
    <div class="print-toolbar"><UButton :to="`/treatments/${id}/edit`" color="neutral" variant="outline" icon="i-lucide-arrow-left">Back to editor</UButton><PrescriptionPrintOptions v-model:a5="printAsA5" v-model:grayscale="printGrayscale" /><UButton type="button" icon="i-lucide-printer" :loading="printPending" @click="printDocument">Print</UButton></div>
    <article class="print-sheet" :class="{ 'print-sheet-a5': printAsA5, 'print-sheet-grayscale': printGrayscale }" dir="ltr" aria-label="Printable treatment prescription">
      <header class="sheet-header" :class="{ 'without-doctor-logo': !doctorLogo }">
        <div class="clinic-brand"><div class="logo-frame"><img v-if="hospitalLogo" :src="hospitalLogo" alt="Hospital logo"><UIcon v-else name="i-lucide-heart-pulse" /></div><p v-if="hospitalName" class="hospital-name" dir="rtl"><bdi>{{ hospitalName }}</bdi></p></div>
        <div v-if="doctorLogo" class="doctor-logo-frame"><img :src="doctorLogo" alt="Doctor logo"></div>
        <div class="doctor-brand"><h2><bdi>{{ doctorName }}</bdi></h2><p v-if="profession"><bdi>{{ profession }}</bdi></p><p class="doctor-user">{{ auth.user.value?.userName }}</p></div>
      </header>

      <section class="patient-box"><h2><UIcon name="i-lucide-user-round" /> Patient information</h2><div class="patient-grid"><dl><div><dt>Name</dt><dd><bdi>{{ treatment.patient.name }}</bdi></dd></div><div><dt>Code</dt><dd dir="ltr">{{ treatment.patient.code }}</dd></div><div><dt>Age / Gender</dt><dd>{{ localNumber(treatment.patient.age) }} years / {{ genders[treatment.patient.gender] || treatment.patient.gender }}</dd></div><div><dt>Phone</dt><dd dir="ltr">{{ treatment.patient.phone || '—' }}</dd></div></dl><dl><div><dt>Date</dt><dd>{{ dateParts[0] }}</dd></div><div><dt>Time</dt><dd dir="ltr">{{ dateParts[1] }}</dd></div><div><dt>Blood pressure</dt><dd dir="ltr">{{ treatment.bloodPressure || '—' }}</dd></div><div><dt>Address</dt><dd><bdi>{{ treatment.patient.address || '—' }}</bdi></dd></div></dl></div></section>

      <template v-if="treatment.prescriptionItems.length">
        <div class="rx-heading"><div class="rx-title"><span class="rx-mark">R<sub>x</sub></span><h2>Prescription</h2></div><p>Prescription no.: <strong dir="ltr">{{ prescriptionNumber }}</strong></p></div>
        <section class="prescription-section" aria-label="Prescription medications"><table><thead><tr><th>#</th><th>Medications</th><th>Type</th><th>Dose</th><th>Frequency</th><th>Duration</th></tr></thead><tbody><tr v-for="item in treatment.prescriptionItems" :key="item.id"><td><span class="number-circle">{{ localNumber(item.position) }}</span></td><td class="medicine-name"><bdi>{{ item.medicationName }}</bdi></td><td>{{ item.medicationType }}</td><td>{{ localNumber(item.dose) }}</td><td>{{ item.frequency }}</td><td>{{ item.duration }}</td></tr></tbody></table></section>
      </template>

      <section v-if="clinicalNotesText" class="notes-box"><h2><UIcon name="i-lucide-clipboard-list" /> Clinical notes</h2><p><bdi>{{ clinicalNotesText }}</bdi></p></section>

      <section v-if="additionalInstructionsText" class="instructions-box" aria-label="Additional instructions"><h2><UIcon name="i-lucide-info" /> Additional instructions</h2><p><bdi>{{ additionalInstructionsText }}</bdi></p></section>

      <footer class="sheet-footer"><div class="signature-block"><div class="signature-line"></div><strong><bdi>{{ doctorName }}</bdi></strong><span v-if="profession"><bdi>{{ profession }}</bdi></span></div><div class="footer-date"><span>Date: {{ dateParts[0] }}</span><span>Doctor signature: ________________</span></div><div v-if="hospitalAddress || hospitalPhoneNumbers" class="hospital-footer"><span v-if="hospitalAddress">Address: <bdi>{{ hospitalAddress }}</bdi></span><span v-if="hospitalPhoneNumbers" dir="ltr">Phone: {{ hospitalPhoneNumbers }}</span></div></footer>
      <div class="green-wave" aria-hidden="true"></div>
    </article>
    </template>
  </div>
</template>

<style scoped>
.print-page { max-width: 210mm; margin-inline: auto; color: #103e35; }
.print-page.print-page-a5 { max-width: 148mm; }
.print-sheet-a5 { width: 148mm; min-height: 210mm; padding: 7mm 7mm 12mm; gap: 7px; font-size: 8pt; }
.print-state { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 20px; border: 1px solid #bfd9cd; border-radius: 12px; background: #f0f7f3; color: #064735; }
.print-state .iconify { width: 20px; height: 20px; }
.print-error { justify-content: space-between; }
.print-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }
.print-sheet { position: relative; display: flex; flex-direction: column; gap: 12px; width: 210mm; min-height: 297mm; box-sizing: border-box; padding: 11mm 10mm 16mm; overflow: hidden; background: #fff; box-shadow: 0 20px 55px #173f3426; font-family: 'Vazirmatn', 'Segoe UI', Arial, sans-serif; font-size: 9.5pt; line-height: 1.4; }
.sheet-header { display: grid; grid-template-columns: minmax(34mm, .9fr) minmax(18mm, 22mm) minmax(48mm, 1fr); gap: 4mm; align-items: center; min-height: 25mm; padding: 0 0 4mm; }
.sheet-header.without-doctor-logo { grid-template-columns: minmax(30mm, 1fr) minmax(48mm, .9fr); }
.clinic-brand { display: flex; flex-direction: column; align-items: center; justify-content: center; min-width: 0; }
.logo-frame { display: grid; place-items: center; width: 22mm; height: 22mm; flex-shrink: 0; color: #07553e; }
.logo-frame img { display: block; width: 100%; height: 100%; object-fit: contain; }
.logo-frame .iconify { width: 18mm; height: 18mm; }
.hospital-name { max-width: 45mm; margin: 1mm 0 0; color: #064735; font-size: 11pt; font-weight: 700; line-height: 1.2; text-align: center; overflow-wrap: anywhere; }
.doctor-logo-frame { display: grid; place-items: center; width: 21mm; height: 21mm; justify-self: center; overflow: hidden; }
.doctor-logo-frame img { display: block; width: 100%; height: 100%; object-fit: contain; }
.doctor-brand { min-width: 0; padding: 0; }
.doctor-brand h2 { margin: 0; color: #064735; font-size: 16pt; line-height: 1.2; overflow-wrap: anywhere; }
.doctor-brand p { margin: 1.5mm 0 0; font-size: 9.5pt; line-height: 1.3; overflow-wrap: anywhere; }
.doctor-user { color: #597268; }
.patient-box, .notes-box, .instructions-box { border: 1px solid #bfd9cd; border-radius: 3mm; background: linear-gradient(110deg, #f0f7f3, #f8fbf8); }
.patient-box { padding: 3mm 4mm; }
.patient-box h2, .notes-box h2, .instructions-box h2 { display: flex; align-items: center; gap: 3mm; margin: 0 0 2mm; color: #064735; font-size: 11pt; }
.patient-box h2 .iconify, .notes-box h2 .iconify, .instructions-box h2 .iconify { width: 6mm; height: 6mm; }
.patient-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7mm; }
.patient-grid > dl { display: grid; gap: 1mm; margin: 0; min-width: 0; }
.patient-grid > dl + dl { padding-inline-start: 7mm; border-inline-start: 1px solid #bfd9cd; }
.patient-grid dl div { display: grid; grid-template-columns: 30mm minmax(0, 1fr); gap: 2mm; min-width: 0; }
.patient-grid dt { font-weight: 500; }
.patient-grid dd { min-width: 0; margin: 0; color: #18394a; overflow-wrap: anywhere; }
.rx-heading { display: flex; align-items: end; justify-content: space-between; flex-wrap: wrap; gap: 4mm; }
.rx-title { display: flex; align-items: baseline; gap: 5mm; }
.rx-mark { color: #064735; font-family: Georgia, serif; font-size: 36pt; font-weight: 700; line-height: .85; }
.rx-mark sub { font-size: 25pt; vertical-align: -.25em; }
.rx-title h2 { margin: 0; color: #064735; font-size: 20pt; line-height: 1; }
.rx-heading p { margin: 0 0 1mm; border-bottom: 1px solid #2e735a; padding-bottom: 2mm; }
.rx-heading strong { margin-inline-start: 3mm; color: #193b49; }
.prescription-section table { width: 100%; border-collapse: collapse; text-align: start; }
.prescription-section th { padding: 2mm 1.5mm; background: #e9f3ec; color: #064735; font-weight: 700; text-align: start; border-inline-end: 1px solid #cee0d4; }
.prescription-section th:first-child { width: 12mm; border-radius: 2mm 0 0 2mm; }
.prescription-section th:nth-child(2) { width: 25%; }
.prescription-section th:last-child { border-inline-end: 0; }
.prescription-section td { padding: 2.5mm 1.5mm; border-bottom: 1px solid #d4e4de; color: #18394a; vertical-align: top; overflow-wrap: anywhere; }
.medicine-name { font-weight: 700; }
.number-circle { display: grid; place-items: center; width: 8mm; height: 8mm; border-radius: 50%; background: #06503d; color: #fff; font-weight: 700; }
.no-medicine { text-align: center; color: #698078 !important; }
.notes-box, .instructions-box { padding: 3mm 4mm; }
.notes-box p, .instructions-box p { margin: 0; color: #18394a; white-space: normal; overflow-wrap: anywhere; }
.write-lines { display: grid; gap: 2mm; }
.write-lines span { height: 3mm; border-bottom: 1px solid #d4e4de; }
.sheet-footer { position: relative; z-index: 1; display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 4mm 10mm; margin-top: auto; padding-bottom: 5mm; }
.signature-block { display: grid; min-width: 45mm; }
.signature-line { width: 60mm; max-width: 100%; height: 10mm; border-bottom: 1px solid #2e735a; }
.signature-block strong { margin-top: 2mm; font-size: 11pt; }
.signature-block span, .footer-date { font-size: 9.5pt; }
.footer-date { display: grid; gap: 5mm; color: #18394a; }
.hospital-footer { grid-column: 1 / -1; display: flex; flex-wrap: wrap; justify-content: center; gap: 2mm 8mm; border-top: 1px solid #bfd9cd; padding-top: 2mm; color: #36594d; font-size: 8pt; line-height: 1.4; text-align: center; overflow-wrap: anywhere; }
.green-wave { position: absolute; z-index: 0; inset-inline: -5mm; bottom: -7mm; height: 20mm; border-top: 2mm solid #91a851; border-radius: 50% 50% 0 0; background: linear-gradient(115deg, #064735, #168363); transform: rotate(-2deg); pointer-events: none; }
.print-sheet-grayscale { filter: grayscale(100%); }
@media (max-width: 700px) { .print-sheet { padding: 20px 14px 55px; min-height: 0; gap: 18px; font-size: 12px; }.sheet-header { grid-template-columns: minmax(0, 1fr) minmax(32px, 42px) minmax(0, 1fr); gap: 8px; min-height: 52px; padding-bottom: 8px; }.sheet-header.without-doctor-logo { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }.logo-frame { width: 42px; height: 42px; }.hospital-name { max-width: 100%; margin-top: 3px; font-size: 12px; }.doctor-logo-frame { width: 36px; height: 36px; }.doctor-brand h2 { font-size: 14px; }.doctor-brand p { font-size: 10px; }.patient-grid { gap: 8px; }.patient-grid > dl + dl { padding-inline-start: 8px; }.patient-grid dl div { grid-template-columns: 70px minmax(0, 1fr); }.rx-mark { font-size: 38px; }.rx-mark sub { font-size: 18px; }.rx-title h2 { font-size: 22px; }.prescription-section { overflow-x: auto; }.prescription-section table { min-width: 480px; }.sheet-footer { margin-top: 25px; }.hospital-footer { font-size: 10px; }.green-wave { height: 35px; bottom: -12px; } }
@media print {
  @page { size: A4 portrait; margin: 0; }
  :global(html), :global(body), :global(#__nuxt) { display: block !important; width: auto !important; min-height: 0 !important; margin: 0 !important; padding: 0 !important; overflow: visible !important; background: #fff !important; }
  :global(.print-layout), .print-page { display: block !important; visibility: visible !important; width: auto !important; max-width: none !important; min-height: 0 !important; margin: 0 !important; padding: 0 !important; overflow: visible !important; background: #fff !important; }
  .print-toolbar { display: none !important; }
  .print-sheet, .print-sheet * { visibility: visible !important; }
  .print-sheet { display: flex !important; position: relative; width: 210mm; min-height: 297mm; margin-inline: auto; padding: 11mm 10mm 16mm; gap: 12px; overflow: visible; box-shadow: none; font-size: 9.5pt; print-color-adjust: exact; -webkit-print-color-adjust: exact; }
  :global(.print-layout:has(.print-sheet-a5)) { width: 148mm !important; margin-inline: auto !important; }
  .print-page-a5 { width: 148mm !important; }
  .print-page-a5 .print-sheet { width: 148mm; min-height: 210mm; padding: 7mm 7mm 12mm; gap: 7px; font-size: 8pt; }
  .print-sheet-grayscale { filter: grayscale(100%) !important; }
  .sheet-header, .patient-box, .rx-heading, .notes-box, .instructions-box, .sheet-footer { break-inside: avoid; }
  .prescription-section tr { break-inside: avoid; }
}
</style>
