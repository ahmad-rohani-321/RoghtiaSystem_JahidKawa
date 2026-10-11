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
try { treatment.value = await api.get(id) }
catch (error: unknown) {
  const failure = error as { statusCode?: number, status?: number }
  throw createError({ statusCode: (failure.statusCode || failure.status) === 404 ? 404 : 503, statusMessage: 'تداوي را نه وړل شوه.' })
}
try { await doctor.load() } catch { /* The prescription remains printable without optional branding. */ }

const info = computed(() => doctor.information.value)
const doctorName = computed(() => info.value?.doctorNameEnglish || info.value?.doctorNamePashto || auth.user.value?.userName || 'ډاکټر')
const profession = computed(() => info.value?.doctorProfessionEnglish || info.value?.doctorProfessionPashto || '')
const clinicName = computed(() => info.value?.hospitalNamePashto || 'روغتیا')
const logo = computed(() => info.value?.hospitalLogo || info.value?.doctorLogo || null)
const genders: Record<string, string> = { Male: 'نارینه', Female: 'ښځینه', Other: 'نور' }
const dateParts = computed(() => (treatment.value ? formatJalaliDateTime(treatment.value.recordedAtUtc) : '').split('، '))
const prescriptionNumber = computed(() => {
  if (!treatment.value) return ''
  const year = new Date(treatment.value.recordedAtUtc).getUTCFullYear()
  return `RX-${year}-${String(treatment.value.id).padStart(6, '0')}`
})
const printDocument = () => window.print()

useHead({ title: `نسخه ${prescriptionNumber.value} — روغتیا` })

onMounted(async () => {
  if (route.query.auto !== '1' || !treatment.value) return
  await nextTick()
  await document.fonts.ready
  await Promise.all(Array.from(document.querySelectorAll('.print-sheet img')).map(image => (image as HTMLImageElement).decode().catch(() => {})))
  window.print()
})
</script>

<template>
  <div v-if="treatment" class="print-page">
    <div class="print-toolbar"><UButton :to="`/treatments/${id}/edit`" color="neutral" variant="outline" icon="i-lucide-arrow-right">سمون ته بېرته</UButton><UButton type="button" icon="i-lucide-printer" @click="printDocument">چاپ</UButton></div>
    <article class="print-sheet" dir="rtl" aria-label="د تداوۍ چاپېدونکې نسخه">
      <header class="sheet-header">
        <div class="clinic-brand"><div class="logo-frame"><img v-if="logo" :src="logo" alt="د روغتون نښه"><UIcon v-else name="i-lucide-heart-pulse" /></div><div><h1><bdi>{{ clinicName }}</bdi></h1><p>د روغتیا او درملنې مرکز</p></div></div>
        <div class="doctor-brand"><h2><bdi>{{ doctorName }}</bdi></h2><p v-if="profession"><bdi>{{ profession }}</bdi></p><p class="doctor-user">{{ auth.user.value?.userName }}</p></div>
      </header>

      <section class="patient-box"><h2><UIcon name="i-lucide-user-round" /> د ناروغ معلومات</h2><div class="patient-grid"><dl><div><dt>نوم</dt><dd><bdi>{{ treatment.patient.name }}</bdi></dd></div><div><dt>کوډ</dt><dd dir="ltr">{{ treatment.patient.code }}</dd></div><div><dt>عمر / جنسیت</dt><dd>{{ localNumber(treatment.patient.age) }} کاله / {{ genders[treatment.patient.gender] || treatment.patient.gender }}</dd></div><div><dt>ټیلیفون</dt><dd dir="ltr">{{ treatment.patient.phone || '—' }}</dd></div></dl><dl><div><dt>نېټه</dt><dd>{{ dateParts[0] }}</dd></div><div><dt>وخت</dt><dd dir="ltr">{{ dateParts[1] }}</dd></div><div><dt>د وینې فشار</dt><dd dir="ltr">{{ treatment.bloodPressure || '—' }}</dd></div><div><dt>پته</dt><dd><bdi>{{ treatment.patient.address || '—' }}</bdi></dd></div></dl></div></section>

      <div class="rx-heading"><div class="rx-title"><span class="rx-mark">R<sub>x</sub></span><h2>نسخه</h2></div><p>د نسخې شمېره: <strong dir="ltr">{{ prescriptionNumber }}</strong></p></div>
      <section class="prescription-section" aria-label="د نسخې درمل"><table><thead><tr><th>#</th><th>درمل</th><th>ډول</th><th>موده</th><th>تکرار</th></tr></thead><tbody><tr v-for="item in treatment.prescriptionItems" :key="item.id"><td><span class="number-circle">{{ localNumber(item.position) }}</span></td><td class="medicine-name"><bdi>{{ item.medicationName }}</bdi></td><td>{{ item.medicationType }}</td><td>{{ item.duration }}</td><td>{{ localNumber(item.refills) }}</td></tr><tr v-if="!treatment.prescriptionItems.length"><td colspan="5" class="no-medicine">د دې تداوۍ لپاره درمل نه دي لیکل شوي.</td></tr></tbody></table></section>

      <section class="notes-box"><h2><UIcon name="i-lucide-clipboard-list" /> کلینیکي یادښتونه</h2><ul v-if="treatment.clinicalNotes.length"><li v-for="note in treatment.clinicalNotes" :key="note.id"><bdi>{{ note.text }}</bdi></li></ul><p v-else>—</p></section>

      <section class="instructions-box" aria-label="د لاس لیکلو لپاره د لارښوونو ځای"><h2><UIcon name="i-lucide-info" /> اضافي لارښوونې</h2><div class="write-lines" aria-hidden="true"><span></span><span></span></div></section>

      <footer class="sheet-footer"><div class="signature-block"><div class="signature-line"></div><strong><bdi>{{ doctorName }}</bdi></strong><span v-if="profession"><bdi>{{ profession }}</bdi></span></div><div class="footer-date"><span>نېټه: {{ dateParts[0] }}</span><span>د ډاکټر لاسلیک: ________________</span></div></footer>
      <div class="green-wave" aria-hidden="true"></div>
    </article>
  </div>
</template>

<style scoped>
.print-page { max-width: 210mm; margin-inline: auto; color: #103e35; }
.print-toolbar { display: flex; justify-content: space-between; gap: 12px; margin-bottom: 16px; }
.print-sheet { position: relative; display: flex; flex-direction: column; gap: 21px; min-height: 297mm; padding: 18mm 11mm 22mm; overflow: hidden; background: #fff; box-shadow: 0 20px 55px #173f3426; font-family: 'Noto Sans Arabic', 'Segoe UI', sans-serif; font-size: 10.5pt; line-height: 1.5; }
.sheet-header { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, .75fr); gap: 12mm; align-items: center; min-height: 34mm; }
.clinic-brand { display: flex; align-items: center; gap: 6mm; min-width: 0; }
.logo-frame { display: grid; place-items: center; width: 26mm; height: 26mm; flex-shrink: 0; color: #07553e; }
.logo-frame img { max-width: 100%; max-height: 100%; object-fit: contain; }
.logo-frame .iconify { width: 18mm; height: 18mm; }
.clinic-brand h1 { margin: 0; color: #064735; font-size: 21pt; font-weight: 800; line-height: 1.25; overflow-wrap: anywhere; }
.clinic-brand p { margin: 2mm 0 0; color: #3d6757; letter-spacing: .03em; }
.doctor-brand { min-width: 0; padding-inline-start: 8mm; border-inline-start: 1px solid #2d7960; }
.doctor-brand h2 { margin: 0; color: #064735; font-size: 16pt; line-height: 1.3; overflow-wrap: anywhere; }
.doctor-brand p { margin: 2mm 0 0; font-size: 10pt; overflow-wrap: anywhere; }
.doctor-user { color: #597268; }
.patient-box, .notes-box, .instructions-box { border: 1px solid #bfd9cd; border-radius: 3mm; background: linear-gradient(110deg, #f0f7f3, #f8fbf8); }
.patient-box { padding: 4mm 5mm; }
.patient-box h2, .notes-box h2, .instructions-box h2 { display: flex; align-items: center; gap: 3mm; margin: 0 0 3mm; color: #064735; font-size: 12pt; }
.patient-box h2 .iconify, .notes-box h2 .iconify, .instructions-box h2 .iconify { width: 6mm; height: 6mm; }
.patient-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7mm; }
.patient-grid > dl { display: grid; gap: 2mm; margin: 0; min-width: 0; }
.patient-grid > dl + dl { padding-inline-start: 7mm; border-inline-start: 1px solid #bfd9cd; }
.patient-grid dl div { display: grid; grid-template-columns: 30mm minmax(0, 1fr); gap: 2mm; min-width: 0; }
.patient-grid dt { font-weight: 500; }
.patient-grid dd { min-width: 0; margin: 0; color: #18394a; overflow-wrap: anywhere; }
.rx-heading { display: flex; align-items: end; justify-content: space-between; flex-wrap: wrap; gap: 4mm; }
.rx-title { display: flex; align-items: baseline; gap: 5mm; }
.rx-mark { color: #064735; font-family: Georgia, serif; font-size: 44pt; font-weight: 700; line-height: .85; }
.rx-mark sub { font-size: 25pt; vertical-align: -.25em; }
.rx-title h2 { margin: 0; color: #064735; font-size: 24pt; line-height: 1; }
.rx-heading p { margin: 0 0 1mm; border-bottom: 1px solid #2e735a; padding-bottom: 2mm; }
.rx-heading strong { margin-inline-start: 3mm; color: #193b49; }
.prescription-section table { width: 100%; border-collapse: collapse; text-align: start; }
.prescription-section th { padding: 3mm 2mm; background: #e9f3ec; color: #064735; font-weight: 700; text-align: start; border-inline-end: 1px solid #cee0d4; }
.prescription-section th:first-child { width: 12mm; border-radius: 2mm 0 0 2mm; }
.prescription-section th:nth-child(2) { width: 40%; }
.prescription-section th:last-child { border-inline-end: 0; }
.prescription-section td { padding: 4mm 2mm; border-bottom: 1px solid #d4e4de; color: #18394a; vertical-align: top; overflow-wrap: anywhere; }
.medicine-name { font-weight: 700; }
.number-circle { display: grid; place-items: center; width: 8mm; height: 8mm; border-radius: 50%; background: #06503d; color: #fff; font-weight: 700; }
.no-medicine { text-align: center; color: #698078 !important; }
.notes-box, .instructions-box { padding: 4mm 5mm; }
.notes-box ul { margin: 0; padding-inline-start: 7mm; color: #18394a; }
.notes-box li { margin: 1.5mm 0; overflow-wrap: anywhere; }
.notes-box p { margin: 0; }
.write-lines { display: grid; gap: 4mm; }
.write-lines span { height: 4mm; border-bottom: 1px solid #d4e4de; }
.sheet-footer { display: flex; justify-content: space-between; align-items: end; gap: 10mm; margin-top: auto; padding-bottom: 5mm; }
.signature-block { display: grid; min-width: 45mm; }
.signature-line { width: 60mm; max-width: 100%; height: 15mm; border-bottom: 1px solid #2e735a; }
.signature-block strong { margin-top: 2mm; font-size: 11pt; }
.signature-block span, .footer-date { font-size: 9.5pt; }
.footer-date { display: grid; gap: 5mm; color: #18394a; }
.green-wave { position: absolute; inset-inline: -5mm; bottom: -7mm; height: 20mm; border-top: 2mm solid #91a851; border-radius: 50% 50% 0 0; background: linear-gradient(115deg, #064735, #168363); transform: rotate(-2deg); }
@media (max-width: 700px) { .print-sheet { padding: 20px 14px 55px; min-height: 0; gap: 18px; font-size: 12px; }.sheet-header { gap: 12px; }.clinic-brand { gap: 8px; }.logo-frame { width: 38px; height: 38px; }.clinic-brand h1 { font-size: 18px; }.clinic-brand p { font-size: 10px; }.doctor-brand { padding-inline-start: 12px; }.doctor-brand h2 { font-size: 14px; }.doctor-brand p { font-size: 10px; }.patient-grid { gap: 8px; }.patient-grid > dl + dl { padding-inline-start: 8px; }.patient-grid dl div { grid-template-columns: 70px minmax(0, 1fr); }.rx-mark { font-size: 38px; }.rx-mark sub { font-size: 18px; }.rx-title h2 { font-size: 22px; }.prescription-section { overflow-x: auto; }.prescription-section table { min-width: 480px; }.sheet-footer { margin-top: 25px; }.green-wave { height: 35px; bottom: -12px; } }
@media print { @page { size: A4 portrait; margin: 0; } .print-page { max-width: none; }.print-toolbar { display: none !important; }.print-sheet { width: 210mm; min-height: 297mm; padding: 18mm 11mm 22mm; gap: 21px; overflow: visible; box-shadow: none; font-size: 10.5pt; print-color-adjust: exact; -webkit-print-color-adjust: exact; }.sheet-header, .patient-box, .rx-heading, .notes-box, .instructions-box, .sheet-footer { break-inside: avoid; }.prescription-section tr { break-inside: avoid; } }
</style>
