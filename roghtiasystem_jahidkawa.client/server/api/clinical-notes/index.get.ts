import type { ClinicalNote } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<ClinicalNote[]>(event, '/api/clinical-notes'))._data
})
