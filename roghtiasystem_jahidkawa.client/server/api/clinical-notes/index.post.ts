import type { ClinicalNote } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  const input = await readClinicalNoteInput(event)
  return (await requestOwnedApi<ClinicalNote>(event, '/api/clinical-notes', { method: 'POST', body: input }))._data
})
