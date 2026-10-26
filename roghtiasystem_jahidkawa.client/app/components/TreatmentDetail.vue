<script setup lang="ts">
import type { Treatment } from '#shared/types/treatment'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ treatment: Treatment | null }>()
const genders: Record<string, string> = { Male: 'Male', Female: 'Female', Other: 'Other' }
const recordedDate = computed(() => props.treatment ? formatJalaliDateTime(props.treatment.recordedAtUtc) : '')
</script>

<template>
  <UModal v-model:open="open" title="Treatment details" description="Saved information and prescription" :content="{ dir: 'ltr' }" :ui="{ content: 'w-[min(880px,calc(100vw-1.5rem))] max-w-none min-w-0', body: 'min-w-0' }">
    <template #body>
      <div v-if="treatment" class="treatment-detail">
        <div class="detail-header"><div><h3><bdi>{{ treatment.patient.name }}</bdi></h3><p>{{ treatment.patient.code }} · {{ recordedDate }}</p></div><span class="detail-badge">Treatments #{{ localNumber(treatment.id) }}</span></div>
        <dl class="detail-facts">
          <div><dt>Age</dt><dd>{{ localNumber(treatment.patient.age) }} years</dd></div>
          <div><dt>Gender</dt><dd>{{ genders[treatment.patient.gender] || treatment.patient.gender }}</dd></div>
          <div><dt>Phone</dt><dd dir="ltr">{{ treatment.patient.phone || '—' }}</dd></div>
          <div><dt>Blood pressure</dt><dd dir="ltr">{{ treatment.bloodPressure || '—' }}</dd></div>
        </dl>
        <div v-if="treatment.clinicalNotes.length" class="detail-notes"><h4>Clinical notes</h4><div><span v-for="note in treatment.clinicalNotes" :key="note.id">{{ note.text }}</span></div></div>
        <div v-if="treatment.additionalInstructions" class="detail-notes"><h4>Additional instructions</h4><p class="additional-instructions">{{ treatment.additionalInstructions }}</p></div>
        <div v-if="treatment.diagnostics?.length || (!treatment.diagnostics?.length && !treatment.investigations?.length && treatment.diagnosticInvestigations.length)" class="detail-notes"><h4>Diagnostics</h4><div><span v-for="item in treatment.diagnostics?.length || treatment.investigations?.length ? treatment.diagnostics || [] : treatment.diagnosticInvestigations" :key="item.id">{{ item.text }}</span></div></div>
        <div v-if="treatment.investigations?.length" class="detail-notes"><h4>Investigations</h4><div><span v-for="item in treatment.investigations" :key="item.id">{{ item.text }}</span></div></div>
        <div class="detail-medicines"><h4>Prescription</h4><div v-if="treatment.prescriptionItems.length" class="detail-table-wrap"><table><thead><tr><th>No.</th><th>Medications</th><th>Type</th><th>Dose</th><th>Frequency</th><th>Duration</th></tr></thead><tbody><tr v-for="item in treatment.prescriptionItems" :key="item.id"><td>{{ localNumber(item.position) }}</td><td><bdi>{{ item.medicationName }}</bdi></td><td>{{ item.medicationType }}</td><td>{{ localNumber(item.dose) }}</td><td>{{ item.frequency }}</td><td>{{ item.duration }}</td></tr></tbody></table></div><p v-else class="detail-empty">No medications prescribed for this treatment.</p></div>
      </div>
    </template>
  </UModal>
</template>

<style scoped>
.treatment-detail { display: grid; gap: 22px; min-width: 0; font-size: 1rem; }
.detail-header { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 12px; }
.detail-header h3 { margin: 0; font-size: 1.25rem; }
.detail-header p { margin: 4px 0 0; color: var(--muted); }
.detail-badge, .detail-notes span { background: var(--soft); color: var(--teal); padding: 6px 12px; border-radius: 10px; }
.detail-facts { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; margin: 0; }
.detail-facts > div { min-width: 0; border: 1px solid var(--line); border-radius: 12px; padding: 12px; }
.detail-facts dt { color: var(--muted); font-size: .875rem; }
.detail-facts dd { margin: 5px 0 0; overflow-wrap: anywhere; }
h4 { margin: 0 0 12px; font-size: 1rem; }
.detail-notes > div { display: flex; gap: 8px; flex-wrap: wrap; }
.detail-notes span { overflow-wrap: anywhere; }
.additional-instructions { margin: 0; padding: 10px 12px; border-radius: 10px; background: var(--soft); white-space: pre-line; overflow-wrap: anywhere; }
.detail-table-wrap { overflow-x: auto; }
table { width: 100%; min-width: 530px; border-collapse: collapse; text-align: start; }
th, td { padding: 12px; border-bottom: 1px solid var(--line); text-align: start; }
th { color: var(--muted); font-weight: 500; }
.detail-empty { color: var(--muted); }
@media (max-width: 640px) { .detail-facts { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
</style>
