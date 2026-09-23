<script setup lang="ts">
import type { Patient, PatientPage } from '#shared/types/patient'

useHead({ title: 'ناروغان — روغتیا' })

const api = usePatients()
const auth = useAuth()
const toast = useToast()
const nuxtApp = useNuxtApp()
const search = ref('')
const appliedSearch = ref('')
const page = ref(1)
const genders: Record<string, string> = { Male: 'نارینه', Female: 'ښځینه', Other: 'نور' }
const pageSize = 10
const result = ref<PatientPage>({ items: [], total: 0, page: 1, pageSize })
const loading = ref(true)
const loaded = ref(false)
const serviceMessage = ref('')
const formOpen = ref(false)
const selected = ref<Patient | null>(null)
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
  if (import.meta.client) toast.add({ title: 'بیا ننوځئ', description: 'ستاسې ناسته پای ته رسېدلې ده. بیا خپل حساب ته ننوځئ.', color: 'warning' })
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
      ? 'ډېرې هڅې شوې دي. لږ وروسته بیا هڅه وکړئ.'
      : status === 403
        ? 'تاسې د دې معلوماتو د لیدلو اجازه نه لرئ.'
        : 'د ناروغانو لېست را نه وړل شو. مهرباني وکړئ بیا هڅه وکړئ.'
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

function addPatient() {
  ++editSequence
  editingId.value = null
  selected.value = null
  formOpen.value = true
}

async function editPatient(patient: Patient) {
  if (editingId.value !== null) return
  const sequence = ++editSequence
  const userId = auth.user.value?.id
  editingId.value = patient.id
  try {
    const fresh = await api.get(patient.id)
    if (sequence !== editSequence || userId !== auth.user.value?.id) return
    selected.value = fresh
    formOpen.value = true
  } catch (error: unknown) {
    if (sequence !== editSequence || userId !== auth.user.value?.id) return
    const failure = error as { statusCode?: number, status?: number }
    const status = failure.statusCode || failure.status
    if (status === 401) return await handleExpiredSession()
    toast.add({ title: 'معلومات را نه وړل شول', description: status === 404 ? 'دا ناروغان ونه موندل شول. لېست تازه کړئ.' : 'مهرباني وکړئ بیا هڅه وکړئ.', color: 'error' })
  } finally {
    if (sequence === editSequence) editingId.value = null
  }
}

async function onSaved(patient: Patient, created: boolean) {
  if (patient.user.id !== auth.user.value?.id) return
  if (created) {
    clearTimeout(searchTimer)
    search.value = ''
    appliedSearch.value = ''
    page.value = 1
    loaded.value = false
  } else {
    result.value.items = result.value.items.map(item => item.id === patient.id ? patient : item)
  }
  // Reload errors are separate from the successful save notification.
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
  <section class="patients-page" aria-labelledby="patients-title">
    <header class="page-heading">
      <div>
        <div class="eyebrow"><span /> ستاسې د ناروغانو لېست</div>
        <h1 id="patients-title">ناروغان</h1>
        <p>ناروغان ثبت کړئ، معلومات یې وګورئ او تازه یې وساتئ.</p>
      </div>
      <UButton type="button" icon="i-lucide-plus" size="lg" class="add-patient-button" @click="addPatient">نوی ناروغ</UButton>
    </header>

    <section class="panel patients-panel" aria-labelledby="patients-list-title" :aria-busy="loading">
      <header class="patients-toolbar">
        <div class="patients-list-heading">
          <span class="patients-icon" aria-hidden="true"><UIcon name="i-lucide-users-round" /></span>
          <div>
            <h2 id="patients-list-title">{{ appliedSearch ? 'د لټون پایلې' : 'ثبت شوي ناروغان' }}</h2>
            <p aria-live="polite">{{ loaded ? `${localNumber(result.total)} ناروغان` : 'ستاسې د حساب ناروغان' }}</p>
          </div>
        </div>
        <form class="patients-search" role="search" @submit.prevent="applySearch">
          <UInput v-model="search" name="patientSearch" icon="i-lucide-search" placeholder="د نوم، کوډ یا ټیلیفون له مخې لټون" aria-label="د ناروغانو لټون" :maxlength="200" size="xl" class="w-full" :ui="{ base: 'patients-search-input' }" />
          <UButton type="submit" size="lg" color="neutral" variant="outline" :loading="loading" aria-label="ناروغان ولټوئ">لټون</UButton>
        </form>
      </header>

      <div v-if="serviceMessage" class="patients-error" role="alert">
        <UIcon name="i-lucide-circle-alert" aria-hidden="true" />
        <p>{{ serviceMessage }}</p>
        <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-refresh-cw" :loading="loading" :disabled="loading" @click="load">بیا هڅه وکړئ</UButton>
      </div>

      <div v-if="loading" class="patients-state" role="status">
        <UIcon name="i-lucide-loader-circle" class="animate-spin" aria-hidden="true" />
        <p>ناروغان را اخیستل کېږي…</p>
      </div>
      <template v-else-if="loaded && result.items.length">
        <div class="patients-table-wrap">
          <table class="patients-table">
            <caption class="sr-only">ستاسې د ناروغانو معلومات</caption>
            <thead><tr><th scope="col">شمېره</th><th scope="col">د ناروغانو نوم</th><th scope="col">کوډ</th><th scope="col">ټیلیفون</th><th scope="col">عمر</th><th scope="col">جنسیت</th><th scope="col">پته</th><th scope="col"><span class="sr-only">سمون</span></th></tr></thead>
            <tbody>
              <tr v-for="patient in result.items" :key="patient.id">
                <td class="patient-id">{{ localNumber(patient.id) }}</td>
                <th scope="row"><bdi>{{ patient.name }}</bdi></th>
                <td><bdi>{{ patient.code }}</bdi></td>
                <td><bdi dir="ltr">{{ patient.phone || '—' }}</bdi></td>
                <td><span class="patient-quantity">{{ localNumber(patient.age) }}</span></td>
                <td>{{ genders[patient.gender] }}</td>
                <td><p class="patient-remarks" dir="auto">{{ patient.address || '—' }}</p></td>
                <td><UButton type="button" color="neutral" variant="ghost" icon="i-lucide-pencil-line" size="lg" :loading="editingId === patient.id" :disabled="editingId !== null" :aria-label="`${patient.name}: سمون`" @click="editPatient(patient)">سمون</UButton></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="patients-cards">
          <article v-for="patient in result.items" :key="patient.id" class="patient-card">
            <header>
              <div><p class="patient-id">شمېره {{ localNumber(patient.id) }}</p><h3><bdi>{{ patient.name }}</bdi></h3></div>
              <UButton type="button" color="neutral" variant="outline" icon="i-lucide-pencil-line" size="lg" :loading="editingId === patient.id" :disabled="editingId !== null" :aria-label="`${patient.name}: سمون`" @click="editPatient(patient)">سمون</UButton>
            </header>
            <dl>
              <div><dt>کوډ</dt><dd><bdi>{{ patient.code }}</bdi></dd></div>
              <div><dt>ټیلیفون</dt><dd><bdi dir="ltr">{{ patient.phone || '—' }}</bdi></dd></div>
              <div><dt>جنسیت</dt><dd>{{ genders[patient.gender] }}</dd></div>
              <div><dt>عمر</dt><dd class="patient-quantity">{{ localNumber(patient.age) }}</dd></div>
              <div v-if="patient.address" class="patient-card-remarks"><dt>پته</dt><dd dir="auto">{{ patient.address }}</dd></div>
            </dl>
          </article>
        </div>

        <footer class="patients-pagination">
          <p>{{ localNumber(rangeStart) }}–{{ localNumber(rangeEnd) }} له {{ localNumber(result.total) }} ناروغانو څخه</p>
          <nav aria-label="د ناروغانو د پاڼو ټاکل">
            <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-chevron-right" :disabled="loading || page <= 1" aria-label="مخکینۍ پاڼه" @click="changePage(page - 1)" />
            <span aria-live="polite">{{ localNumber(page) }} / {{ localNumber(totalPages) }}</span>
            <UButton type="button" size="lg" color="neutral" variant="outline" icon="i-lucide-chevron-left" :disabled="loading || page >= totalPages" aria-label="راتلونکې پاڼه" @click="changePage(page + 1)" />
          </nav>
        </footer>
      </template>
      <div v-else-if="loaded && !serviceMessage" class="patients-state patients-empty">
        <span class="patients-empty-icon"><UIcon :name="appliedSearch ? 'i-lucide-search' : 'i-lucide-users-round'" aria-hidden="true" /></span>
        <h3>{{ appliedSearch ? 'ناروغان ونه موندل شول' : 'لا ناروغان نه دي ثبت شوي' }}</h3>
        <p>{{ appliedSearch ? 'بل نوم، کوډ یا ټیلیفون ولیکئ او بیا لټون وکړئ.' : 'خپل لومړی ناروغ ثبت کړئ.' }}</p>
        <UButton v-if="appliedSearch" type="button" size="lg" color="neutral" variant="outline" @click="search = ''; applySearch()">ټول ناروغان وښایئ</UButton>
        <UButton v-else type="button" size="lg" icon="i-lucide-plus" @click="addPatient">نوی ناروغ</UButton>
      </div>
    </section>

    <PatientForm v-model:open="formOpen" :patient="selected" @saved="onSaved" />
  </section>
</template>

<style scoped>
.patients-page { width: 100%; min-width: 0; max-width: 1250px; margin-inline: auto; }
.patients-page .page-heading > div { min-width: 0; }
.patients-page .page-heading p { font-size: 1rem; line-height: 1.9; }
.add-patient-button { flex-shrink: 0; min-height: 46px; font-size: 1rem; justify-content: center; }
.patients-panel { min-width: 0; border-radius: 16px; overflow: hidden; }
.patients-toolbar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 24px; padding: 24px; border-bottom: 1px solid var(--line); }
.patients-list-heading { display: flex; align-items: center; gap: 13px; min-width: 0; }
.patients-list-heading > div { min-width: 0; }
.patients-icon { display: grid; place-items: center; width: 46px; height: 46px; flex-shrink: 0; border-radius: 13px; color: var(--teal); background: var(--soft); }
.patients-icon .iconify { width: 24px; height: 24px; }
.patients-list-heading h2 { margin: 0 0 3px; font-size: 1.125rem; font-weight: 650; }
.patients-list-heading p { margin: 0; font-size: 0.875rem; color: var(--muted); }
.patients-search { display: flex; align-items: center; gap: 10px; width: min(100%, 450px); min-width: 0; }
.patients-search > :first-child { min-width: 0; flex: 1; }
.patients-search > :last-child { flex-shrink: 0; min-height: 46px; font-size: 1rem; }
.patients-search :deep(.patients-search-input) { min-width: 0; min-height: 46px; font-size: 1rem; }
.patients-error { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin: 20px 24px; padding: 16px; background: var(--soft); border-radius: 10px; }
.patients-error > .iconify { flex-shrink: 0; color: var(--teal); }
.patients-error p { flex: 1 1 230px; min-width: 0; margin: 0; font-size: 1rem; line-height: 1.9; overflow-wrap: anywhere; }
.patients-error > button { min-height: 44px; font-size: 1rem; white-space: normal; }
.patients-state { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 16px; min-height: 300px; padding: 36px 24px; text-align: center; }
.patients-state > .iconify { width: 30px; height: 30px; color: var(--teal); }
.patients-state h3 { margin: 0; font-size: 1.125rem; font-weight: 600; }
.patients-state p { max-width: 450px; margin: 0; font-size: 1rem; line-height: 1.9; color: var(--muted); }
.patients-state > button { margin-top: 4px; min-height: 44px; font-size: 1rem; }
.patients-empty-icon { display: grid; place-items: center; width: 64px; height: 64px; margin-bottom: 4px; background: var(--soft); border-radius: 20px; color: var(--teal); }
.patients-empty-icon .iconify { width: 30px; height: 30px; }
.patients-table-wrap { width: 100%; min-width: 0; }
.patients-table { width: 100%; table-layout: fixed; border-collapse: collapse; text-align: start; font-size: 1rem; }
.patients-table thead { background: var(--canvas); }
.patients-table th, .patients-table td { padding: 17px 12px; border-bottom: 1px solid var(--line); overflow-wrap: anywhere; vertical-align: middle; }
.patients-table thead th { color: var(--muted); font-size: 0.875rem; font-weight: 500; }
.patients-table th:first-child, .patients-table td:first-child { width: 7%; padding-inline-start: 24px; }
.patients-table th:nth-child(2) { width: 19%; }
.patients-table th:nth-child(3) { width: 12%; }
.patients-table th:nth-child(4) { width: 15%; }
.patients-table th:nth-child(5) { width: 7%; }
.patients-table th:last-child, .patients-table td:last-child { width: 16%; padding-inline-end: 20px; }
.patients-table th:nth-child(6) { width: 10%; }
.patients-table th:nth-child(7) { width: 18%; }
.patients-table tbody th { font-weight: 600; }
.patients-table tbody tr:last-child > * { border-bottom: 0; }
.patients-table tbody tr:hover { background: var(--canvas); }
.patients-table button { min-height: 44px; font-size: 1rem; white-space: normal; }
.patient-id { color: var(--muted); font-size: 0.875rem; }
.patient-type { display: inline-block; max-width: 100%; background: var(--soft); color: var(--teal); border-radius: 7px; padding: 4px 9px; font-size: 0.875rem; }
.patient-quantity { font-weight: 650; font-variant-numeric: tabular-nums; }
.patient-remarks { margin: 0; white-space: pre-wrap; font-size: 0.875rem; line-height: 1.9; }
.patients-cards { display: none; }
.patients-pagination { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 18px; border-top: 1px solid var(--line); padding: 18px 24px; }
.patients-pagination > p { margin: 0; color: var(--muted); font-size: 0.875rem; }
.patients-pagination nav { display: flex; align-items: center; gap: 14px; }
.patients-pagination nav > span { font-size: 1rem; font-variant-numeric: tabular-nums; }
.patients-pagination nav > button { min-width: 44px; min-height: 44px; justify-content: center; }
@media (max-width: 1100px) {
  .patients-table-wrap { display: none; }
  .patients-cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; padding: 20px; }
  .patient-card { min-width: 0; border: 1px solid var(--line); border-radius: 12px; padding: 18px; }
  .patient-card header { display: flex; align-items: flex-start; gap: 12px; justify-content: space-between; }
  .patient-card header > div { min-width: 0; }
  .patient-card header p { margin: 0 0 5px; }
  .patient-card h3 { margin: 0; font-size: 1.125rem; font-weight: 650; line-height: 1.8; overflow-wrap: anywhere; }
  .patient-card header > button { flex-shrink: 0; min-height: 44px; font-size: 1rem; }
  .patient-card dl { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 17px; margin: 18px 0 0; padding-top: 16px; border-top: 1px solid var(--line); }
  .patient-card dl > div { min-width: 0; }
  .patient-card dt { color: var(--muted); font-size: 0.875rem; margin-bottom: 5px; }
  .patient-card dd { margin: 0; font-size: 1rem; overflow-wrap: anywhere; }
  .patient-card-remarks { grid-column: 1 / -1; }
  .patient-card-remarks dd { white-space: pre-wrap; line-height: 1.9; }
}
@media (max-width: 600px) {
  .patients-toolbar { padding: 20px 16px; gap: 20px; }
  .patients-search { width: 100%; }
  .patients-cards { grid-template-columns: minmax(0, 1fr); gap: 12px; padding: 16px; }
  .patient-card { padding: 16px; }
  .patients-error { margin-inline: 16px; }
  .patients-pagination { padding: 18px 16px; }
  .patients-pagination nav { margin-inline-start: auto; }
}
@media (max-width: 540px) { .add-patient-button { width: 100%; } }
@media (max-width: 360px) {
  .patients-search { flex-wrap: wrap; }
  .patients-search > :first-child { flex-basis: 100%; }
  .patients-search > :last-child { flex: 1; justify-content: center; }
  .patient-card header { flex-wrap: wrap; }
}
</style>
