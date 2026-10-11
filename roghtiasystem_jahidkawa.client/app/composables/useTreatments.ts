import type { ClinicalNote, Treatment, TreatmentInput, TreatmentPage, TreatmentQuery } from '#shared/types/treatment'

export function useTreatments() {
  const requestFetch = useRequestFetch()
  const list = (query: TreatmentQuery = {}) => requestFetch<TreatmentPage>('/api/treatments', { query: { ...query }, retry: 0 })
  const get = (id: number) => requestFetch<Treatment>(`/api/treatments/${encodeURIComponent(id)}`, { retry: 0 })
  const create = (input: TreatmentInput) => $fetch<Treatment>('/api/treatments', {
    method: 'POST', body: input, headers: { 'x-roghtia-request': '1' }, retry: 0
  })
  const update = (id: number, input: TreatmentInput) => $fetch<Treatment>(`/api/treatments/${encodeURIComponent(id)}`, {
    method: 'PUT', body: input, headers: { 'x-roghtia-request': '1' }, retry: 0
  })
  const clinicalNotes = () => requestFetch<ClinicalNote[]>('/api/clinical-notes', { retry: 0 })
  const addClinicalNote = (text: string) => $fetch<ClinicalNote>('/api/clinical-notes', {
    method: 'POST', body: { text }, headers: { 'x-roghtia-request': '1' }, retry: 0
  })
  return { list, get, create, update, clinicalNotes, addClinicalNote }
}
