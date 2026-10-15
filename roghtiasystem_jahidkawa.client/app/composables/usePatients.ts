import type { Patient, PatientInput, PatientPage, PatientQuery } from '#shared/types/patient'
import { notifyDashboardDataChanged } from '~/utils/dashboardRefresh'

export function usePatients() {
  const requestFetch = useRequestFetch()

  // List data stays in the page instance rather than surviving a change of user.
  function list(query: PatientQuery = {}) {
    return requestFetch<PatientPage>('/api/patients', { query: { ...query }, retry: 0 })
  }

  function get(id: number) {
    return requestFetch<Patient>(`/api/patients/${encodeURIComponent(id)}`, { retry: 0 })
  }

  function getByCode(code: string) {
    return requestFetch<Patient>(`/api/patients/by-code/${encodeURIComponent(code)}`, { retry: 0 })
  }

  function inputBody(input: PatientInput) {
    return { name: input.name, phone: input.phone, age: input.age, gender: input.gender, address: input.address }
  }

  async function create(input: PatientInput) {
    const patient = await $fetch<Patient>('/api/patients', {
      method: 'POST', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return patient
  }

  async function update(id: number, input: PatientInput) {
    const patient = await $fetch<Patient>(`/api/patients/${encodeURIComponent(id)}`, {
      method: 'PUT', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return patient
  }

  return { list, get, getByCode, create, update }
}
