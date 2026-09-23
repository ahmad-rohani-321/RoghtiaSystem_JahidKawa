import type { Patient } from '../../../shared/types/patient'

export default defineEventHandler(async (event) => {
  const input = await readPatientInput(event)
  return (await requestOwnedApi<Patient>(event, `/api/patients/${readPatientId(event)}`, { method: 'PUT', body: { ...input } }))._data
})
