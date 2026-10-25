import type { AdditionalInstruction, ClinicalNote, DashboardSummary, Treatment, TreatmentInput, TreatmentPage, TreatmentQuery, TreatmentTemplate } from '#shared/types/treatment'
import { notifyDashboardDataChanged } from '~/utils/dashboardRefresh'

export function useTreatments() {
  const requestFetch = useRequestFetch()
  const list = (query: TreatmentQuery = {}) => requestFetch<TreatmentPage>('/api/treatments', { query: { ...query }, retry: 0 })
  const dashboard = (date: string, period: string) => requestFetch<DashboardSummary>('/api/treatments/dashboard', { query: { date, period }, retry: 0 })
  const get = (id: number) => requestFetch<Treatment>(`/api/treatments/${encodeURIComponent(id)}`, { retry: 0 })
  const templates = () => requestFetch<TreatmentTemplate[]>('/api/treatments/templates', { retry: 0 })
  const create = async (input: TreatmentInput) => {
    const treatment = await $fetch<Treatment>('/api/treatments', {
      method: 'POST', body: input, headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return treatment
  }
  const update = async (id: number, input: TreatmentInput) => {
    const treatment = await $fetch<Treatment>(`/api/treatments/${encodeURIComponent(id)}`, {
      method: 'PUT', body: input, headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    notifyDashboardDataChanged()
    return treatment
  }
  const clinicalNotes = () => requestFetch<ClinicalNote[]>('/api/clinical-notes', { retry: 0 })
  const addClinicalNote = (text: string) => $fetch<ClinicalNote>('/api/clinical-notes', {
    method: 'POST', body: { text }, headers: { 'x-roghtia-request': '1' }, retry: 0
  })
  const additionalInstructions = () => requestFetch<AdditionalInstruction[]>('/api/additional-instructions', { retry: 0 })
  const addAdditionalInstruction = (text: string) => $fetch<AdditionalInstruction>('/api/additional-instructions', {
    method: 'POST', body: { text }, headers: { 'x-roghtia-request': '1' }, retry: 0
  })
  return { list, get, dashboard, templates, create, update, clinicalNotes, addClinicalNote, additionalInstructions, addAdditionalInstruction }
}
