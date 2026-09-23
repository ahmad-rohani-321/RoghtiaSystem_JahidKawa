import type { Patient } from '../../../shared/types/patient'

export default defineEventHandler(async (event) => {
  const input = await readPatientInput(event)
  const response = await requestOwnedApi<Patient>(event, '/api/patients', { method: 'POST', body: { ...input } })
  setResponseStatus(event, 201)
  return response._data
})
