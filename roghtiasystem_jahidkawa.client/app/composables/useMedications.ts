import type { Medication, MedicationInput, MedicationPage, MedicationQuery } from '#shared/types/medication'
import { notifyDashboardDataChanged } from '~/utils/dashboardRefresh'

export function useMedications() {
  const requestFetch = useRequestFetch()

  // List data stays in the page instance rather than surviving a change of user.
  function list(query: MedicationQuery = {}) {
    return requestFetch<MedicationPage>('/api/medications', { query: { ...query }, retry: 0 })
  }

  function get(id: number) {
    return requestFetch<Medication>(`/api/medications/${encodeURIComponent(id)}`, { retry: 0 })
  }

  function inputBody(input: MedicationInput) {
    return { name: input.name, type: input.type, quantity: input.quantity, remarks: input.remarks }
  }

  async function create(input: MedicationInput) {
    const medication = await $fetch<Medication>('/api/medications', {
      method: 'POST', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return medication
  }

  async function createBulk(items: MedicationInput[]) {
    const medications = await $fetch<Medication[]>('/api/medications/bulk', {
      method: 'POST', body: { items: items.map(inputBody) }, headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return medications
  }

  async function update(id: number, input: MedicationInput) {
    const medication = await $fetch<Medication>(`/api/medications/${encodeURIComponent(id)}`, {
      method: 'PUT', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return medication
  }

  return { list, get, create, createBulk, update }
}
