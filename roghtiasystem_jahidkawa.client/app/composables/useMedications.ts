import type { Medication, MedicationInput, MedicationPage, MedicationQuery } from '#shared/types/medication'

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

  function create(input: MedicationInput) {
    return $fetch<Medication>('/api/medications', {
      method: 'POST', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
  }

  function update(id: number, input: MedicationInput) {
    return $fetch<Medication>(`/api/medications/${encodeURIComponent(id)}`, {
      method: 'PUT', body: inputBody(input), headers: { 'x-roghtia-request': '1' }, retry: 0
    })
  }

  return { list, get, create, update }
}
