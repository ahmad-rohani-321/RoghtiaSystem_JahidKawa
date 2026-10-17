<script setup lang="ts">
import type { Medication, MedicationPage } from '#shared/types/medication'

useHead({ title: 'Medications — E-Roghtia' })

const api = useMedications()
const auth = useAuth()
const toast = useToast()
const nuxtApp = useNuxtApp()
const search = ref('')
const appliedSearch = ref('')
const page = ref(1)
const pageSize = 10
const result = ref<MedicationPage>({ items: [], total: 0, page: 1, pageSize })
const loading = ref(true)
const loaded = ref(false)
const serviceMessage = ref('')
const formOpen = ref(false)
const bulkFormOpen = ref(false)
const selected = ref<Medication | null>(null)
const editingId = ref<number | null>(null)
const totalPages = computed(() => Math.max(1, Math.ceil(result.value.total / pageSize)))
const rangeStart = computed(() => result.value.items.length ? (result.value.page - 1) * pageSize + 1 : 0)
const rangeEnd = computed(() => rangeStart.value ? rangeStart.value + result.value.items.length - 1 : 0)
let requestSequence = 0
let editSequence = 0
let searchTimer: ReturnType<typeof setTimeout> | undefined

async function handleExpiredSession() {
  result.value = { items: [], total: 0, page: 1, pageSize }
  formOpen.value = false
  bulkFormOpen.value = false
  if (import.meta.client) toast.add({ title: 'Sign in again', description: 'Your session has expired. Please sign in again.', color: 'warning' })
  try { await auth.refresh() } catch { /* The login route checks authentication again. */ }
  await nuxtApp.runWithContext(() => navigateTo('/login'))
}

async function load() {
  const sequence = ++requestSequence
  const userId = auth.user.value?.id
  loading.value = true
  loaded.value = false
  serviceMessage.value = ''
  try {
    const response = await api.list({ page: page.value, pageSize, search: appliedSearch.value })
    if (sequence !== requestSequence || userId !== auth.user.value?.id) return
    result.value = response
    page.value = response.page
    loaded.value = true
    // An update from another tab can change which records match the last page.
    if (page.value > totalPages.value) {
      page.value = totalPages.value
      await load()
    }
  } catch (error: unknown) {
    if (sequence !== requestSequence || userId !== auth.user.value?.id) return
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) return await handleExpiredSession()
    serviceMessage.value = status === 429
      ? 'Too many attempts. Please try again later.'
      : status === 403
        ? 'You do not have permission to view this information.'
        : 'Unable to load medications. Please try again.'
  } finally {
    if (sequence === requestSequence) loading.value = false
  }
}

function applySearch() {
  clearTimeout(searchTimer)
  appliedSearch.value = search.value.trim()
  page.value = 1
  void load()
}

function changePage(value: number) {
  if (loading.value || value < 1 || value > totalPages.value) return
  page.value = value
  void load()
}

function addMedication() {
  ++editSequence
  editingId.value = null
  selected.value = null
  formOpen.value = true
}

async function editMedication(medication: Medication) {
  if (editingId.value !== null) return
  const sequence = ++editSequence
  const userId = auth.user.value?.id
  editingId.value = medication.id
  try {
    const fresh = await api.get(medication.id)
    if (sequence !== editSequence || userId !== auth.user.value?.id) return
    selected.value = fresh
    formOpen.value = true
  } catch (error: unknown) {
    if (sequence !== editSequence || userId !== auth.user.value?.id) return
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) return await handleExpiredSession()
    toast.add({ title: 'Unable to load information', description: status === 404 ? 'Medication not found. Refresh the list.' : 'Please try again.', color: 'error' })
  } finally {
    if (sequence === editSequence) editingId.value = null
  }
}

async function onSaved(medication: Medication, created: boolean) {
  if (medication.user.id !== auth.user.value?.id) return
  if (created) {
    clearTimeout(searchTimer)
    search.value = ''
    appliedSearch.value = ''
    page.value = 1
    loaded.value = false
  } else {
    result.value.items = result.value.items.map(item => item.id === medication.id ? medication : item)
  }
  // Reload errors are separate from the successful save notification.
  await load()
}

async function onBulkSaved(medications: Medication[]) {
  const userId = auth.user.value?.id
  if (!userId || medications.some(medication => medication.user.id !== userId)) return
  clearTimeout(searchTimer)
  search.value = ''
  appliedSearch.value = ''
  page.value = 1
  loaded.value = false
  await load()
}

watch(search, () => {
  clearTimeout(searchTimer)
  if (search.value.trim() === appliedSearch.value) return
  searchTimer = setTimeout(applySearch, 300)
})

watch(() => auth.user.value?.id, () => {
  ++requestSequence
  ++editSequence
  clearTimeout(searchTimer)
  result.value = { items: [], total: 0, page: 1, pageSize }
  loaded.value = false
  selected.value = null
  formOpen.value = false
  bulkFormOpen.value = false
  editingId.value = null
})

onBeforeUnmount(() => {
  ++requestSequence
  ++editSequence
  clearTimeout(searchTimer)
})

await load()
</script>

<template>
  <section class="medications-page" aria-labelledby="medications-title">
    <header class="page-heading">
      <div>
        <div class="eyebrow"><span /> Your medication list</div>
        <h1 id="medications-title">Medications</h1>
        <p>Register medications, view their details and keep them up to date.</p>
      </div>
      <div class="medication-create-actions">
        <UButton type="button" color="neutral" variant="outline" icon="i-lucide-list-plus" size="lg" class="bulk-medication-button" @click="bulkFormOpen = true">Bulk add</UButton>
        <UButton type="button" icon="i-lucide-plus" size="lg" class="add-medication-button" @click="addMedication">New medication</UButton>
      </div>
    </header>

    <section class="panel medications-panel" aria-labelledby="medications-list-title" :aria-busy="loading">
      <header class="medications-toolbar">
        <div class="medications-list-heading">
          <span class="medications-icon" aria-hidden="true"><UIcon name="i-lucide-pill" /></span>
          <div>
            <h2 id="medications-list-title">{{ appliedSearch ? 'Search results' : 'Registered medications' }}</h2>
            <p aria-live="polite">{{ loaded ? `${localNumber(result.total)} Medications` : 'Your medications' }}</p>
          </div>
        </div>
        <form class="medications-search" role="search" @submit.prevent="applySearch">
          <UInput v-model="search" name="medicationSearch" icon="i-lucide-search" placeholder="Search by name or type" aria-label="Search medications" :maxlength="200" size="xl" class="w-full" :ui="{ base: 'medications-search-input' }" />
          <UButton type="submit" size="lg" color="neutral" variant="outline" :loading="loading" aria-label="Search medications">Search</UButton>
        </form>
      </header>

      <div v-if="serviceMessage" class="medications-error" role="alert">
        <UIcon name="i-lucide-circle-alert" aria-hidden="true" />
        <p>{{ serviceMessage }}</p>
        <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-refresh-cw" :loading="loading" :disabled="loading" @click="load">Try again</UButton>
      </div>

      <div v-if="loading" class="medications-state" role="status">
        <UIcon name="i-lucide-loader-circle" class="animate-spin" aria-hidden="true" />
        <p>Loading medications…</p>
      </div>
      <template v-else-if="loaded && result.items.length">
        <div class="medications-table-wrap">
          <table class="medications-table">
            <caption class="sr-only">Your medication names, types, quantities and remarks</caption>
            <thead><tr><th scope="col">No.</th><th scope="col">Medication name</th><th scope="col">Type</th><th scope="col">Quantity</th><th scope="col">Remarks</th><th scope="col"><span class="sr-only">Edit</span></th></tr></thead>
            <tbody>
              <tr v-for="medication in result.items" :key="medication.id">
                <td class="medication-id">{{ localNumber(medication.id) }}</td>
                <th scope="row"><bdi>{{ medication.name }}</bdi></th>
                <td><span class="medication-type"><bdi>{{ medication.type }}</bdi></span></td>
                <td><span class="medication-quantity">{{ localNumber(medication.quantity) }}</span></td>
                <td><p class="medication-remarks" dir="auto">{{ medication.remarks || '—' }}</p></td>
                <td><UButton type="button" color="neutral" variant="ghost" icon="i-lucide-pencil-line" size="lg" :loading="editingId === medication.id" :disabled="editingId !== null" :aria-label="`${medication.name}: Edit`" @click="editMedication(medication)">Edit</UButton></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="medications-cards">
          <article v-for="medication in result.items" :key="medication.id" class="medication-card">
            <header>
              <div><p class="medication-id">No. {{ localNumber(medication.id) }}</p><h3><bdi>{{ medication.name }}</bdi></h3></div>
              <UButton type="button" color="neutral" variant="outline" icon="i-lucide-pencil-line" size="lg" :loading="editingId === medication.id" :disabled="editingId !== null" :aria-label="`${medication.name}: Edit`" @click="editMedication(medication)">Edit</UButton>
            </header>
            <dl>
              <div><dt>Type</dt><dd><bdi>{{ medication.type }}</bdi></dd></div>
              <div><dt>Quantity</dt><dd class="medication-quantity">{{ localNumber(medication.quantity) }}</dd></div>
              <div v-if="medication.remarks" class="medication-card-remarks"><dt>Remarks</dt><dd dir="auto">{{ medication.remarks }}</dd></div>
            </dl>
          </article>
        </div>

        <footer class="medications-pagination">
          <p>{{ localNumber(rangeStart) }}–{{ localNumber(rangeEnd) }} of {{ localNumber(result.total) }} medications</p>
          <nav aria-label="Medication pagination">
            <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-chevron-left" :disabled="loading || page <= 1" aria-label="Previous page" @click="changePage(page - 1)" />
            <span aria-live="polite">{{ localNumber(page) }} / {{ localNumber(totalPages) }}</span>
            <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-chevron-right" :disabled="loading || page >= totalPages" aria-label="Next page" @click="changePage(page + 1)" />
          </nav>
        </footer>
      </template>
      <div v-else-if="loaded && !serviceMessage" class="medications-state medications-empty">
        <span class="medications-empty-icon"><UIcon :name="appliedSearch ? 'i-lucide-search' : 'i-lucide-pill'" aria-hidden="true" /></span>
        <h3>{{ appliedSearch ? 'No medications found' : 'No medications registered yet' }}</h3>
        <p>{{ appliedSearch ? 'Try another name or type.' : 'Register your first medication with its name, type and quantity.' }}</p>
        <UButton v-if="appliedSearch" type="button" size="lg" color="neutral" variant="outline" @click="search = ''; applySearch()">Show all medications</UButton>
        <UButton v-else type="button" size="lg" icon="i-lucide-plus" @click="addMedication">New medication</UButton>
      </div>
    </section>

    <MedicationForm v-model:open="formOpen" :medication="selected" @saved="onSaved" />
    <MedicationBulkForm v-model:open="bulkFormOpen" @saved="onBulkSaved" />
  </section>
</template>

<style scoped>
.medications-page { width: 100%; min-width: 0; max-width: 1250px; margin-inline: auto; }
.medications-page .page-heading > div { min-width: 0; }
.medications-page .page-heading p { font-size: 1rem; line-height: 1.9; }
.medication-create-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; flex-shrink: 0; }
.medication-create-actions > button { min-height: 46px; font-size: 1rem; justify-content: center; }
.add-medication-button { flex-shrink: 0; }
.medications-panel { min-width: 0; border-radius: 16px; overflow: hidden; }
.medications-toolbar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 24px; padding: 24px; border-bottom: 1px solid var(--line); }
.medications-list-heading { display: flex; align-items: center; gap: 13px; min-width: 0; }
.medications-list-heading > div { min-width: 0; }
.medications-icon { display: grid; place-items: center; width: 46px; height: 46px; flex-shrink: 0; border-radius: 13px; color: var(--teal); background: var(--soft); }
.medications-icon .iconify { width: 24px; height: 24px; }
.medications-list-heading h2 { margin: 0 0 3px; font-size: 1.125rem; font-weight: 650; }
.medications-list-heading p { margin: 0; font-size: 0.875rem; color: var(--muted); }
.medications-search { display: flex; align-items: center; gap: 10px; width: min(100%, 450px); min-width: 0; }
.medications-search > :first-child { min-width: 0; flex: 1; }
.medications-search > :last-child { flex-shrink: 0; min-height: 46px; font-size: 1rem; }
.medications-search :deep(.medications-search-input) { min-width: 0; min-height: 46px; font-size: 1rem; }
.medications-error { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin: 20px 24px; padding: 16px; background: var(--soft); border-radius: 10px; }
.medications-error > .iconify { flex-shrink: 0; color: var(--teal); }
.medications-error p { flex: 1 1 230px; min-width: 0; margin: 0; font-size: 1rem; line-height: 1.9; overflow-wrap: anywhere; }
.medications-error > button { min-height: 44px; font-size: 1rem; white-space: normal; }
.medications-state { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 16px; min-height: 300px; padding: 36px 24px; text-align: center; }
.medications-state > .iconify { width: 30px; height: 30px; color: var(--teal); }
.medications-state h3 { margin: 0; font-size: 1.125rem; font-weight: 600; }
.medications-state p { max-width: 450px; margin: 0; font-size: 1rem; line-height: 1.9; color: var(--muted); }
.medications-state > button { margin-top: 4px; min-height: 44px; font-size: 1rem; }
.medications-empty-icon { display: grid; place-items: center; width: 64px; height: 64px; margin-bottom: 4px; background: var(--soft); border-radius: 20px; color: var(--teal); }
.medications-empty-icon .iconify { width: 30px; height: 30px; }
.medications-table-wrap { width: 100%; min-width: 0; }
.medications-table { width: 100%; table-layout: fixed; border-collapse: collapse; text-align: start; font-size: 1rem; }
.medications-table thead { background: var(--canvas); }
.medications-table th, .medications-table td { padding: 17px 12px; border-bottom: 1px solid var(--line); overflow-wrap: anywhere; vertical-align: middle; }
.medications-table thead th { color: var(--muted); font-size: 0.875rem; font-weight: 500; }
.medications-table th:first-child, .medications-table td:first-child { width: 7%; padding-inline-start: 24px; }
.medications-table th:nth-child(2) { width: 24%; }
.medications-table th:nth-child(3) { width: 16%; }
.medications-table th:nth-child(4) { width: 11%; }
.medications-table th:nth-child(5) { width: 26%; }
.medications-table th:last-child, .medications-table td:last-child { width: 16%; padding-inline-end: 20px; }
.medications-table tbody th { font-weight: 600; }
.medications-table tbody tr:last-child > * { border-bottom: 0; }
.medications-table tbody tr:hover { background: var(--canvas); }
.medications-table button { min-height: 44px; font-size: 1rem; white-space: normal; }
.medication-id { color: var(--muted); font-size: 0.875rem; }
.medication-type { display: inline-block; max-width: 100%; background: var(--soft); color: var(--teal); border-radius: 7px; padding: 4px 9px; font-size: 0.875rem; }
.medication-quantity { font-weight: 650; font-variant-numeric: tabular-nums; }
.medication-remarks { margin: 0; white-space: pre-wrap; font-size: 0.875rem; line-height: 1.9; }
.medications-cards { display: none; }
.medications-pagination { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 18px; border-top: 1px solid var(--line); padding: 18px 24px; }
.medications-pagination > p { margin: 0; color: var(--muted); font-size: 0.875rem; }
.medications-pagination nav { display: flex; align-items: center; gap: 14px; }
.medications-pagination nav > span { font-size: 1rem; font-variant-numeric: tabular-nums; }
.medications-pagination nav > button { min-width: 44px; min-height: 44px; justify-content: center; }
@media (max-width: 1100px) {
  .medications-table-wrap { display: none; }
  .medications-cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; padding: 20px; }
  .medication-card { min-width: 0; border: 1px solid var(--line); border-radius: 12px; padding: 18px; }
  .medication-card header { display: flex; align-items: flex-start; gap: 12px; justify-content: space-between; }
  .medication-card header > div { min-width: 0; }
  .medication-card header p { margin: 0 0 5px; }
  .medication-card h3 { margin: 0; font-size: 1.125rem; font-weight: 650; line-height: 1.8; overflow-wrap: anywhere; }
  .medication-card header > button { flex-shrink: 0; min-height: 44px; font-size: 1rem; }
  .medication-card dl { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 17px; margin: 18px 0 0; padding-top: 16px; border-top: 1px solid var(--line); }
  .medication-card dl > div { min-width: 0; }
  .medication-card dt { color: var(--muted); font-size: 0.875rem; margin-bottom: 5px; }
  .medication-card dd { margin: 0; font-size: 1rem; overflow-wrap: anywhere; }
  .medication-card-remarks { grid-column: 1 / -1; }
  .medication-card-remarks dd { white-space: pre-wrap; line-height: 1.9; }
}
@media (max-width: 600px) {
  .medications-toolbar { padding: 20px 16px; gap: 20px; }
  .medications-search { width: 100%; }
  .medications-cards { grid-template-columns: minmax(0, 1fr); gap: 12px; padding: 16px; }
  .medication-card { padding: 16px; }
  .medications-error { margin-inline: 16px; }
  .medications-pagination { padding: 18px 16px; }
  .medications-pagination nav { margin-inline-start: auto; }
}
@media (max-width: 540px) { .medication-create-actions { width: 100%; } .medication-create-actions > button { flex: 1 1 150px; } }
@media (max-width: 360px) {
  .medications-search { flex-wrap: wrap; }
  .medications-search > :first-child { flex-basis: 100%; }
  .medications-search > :last-child { flex: 1; justify-content: center; }
  .medication-card header { flex-wrap: wrap; }
}
</style>
