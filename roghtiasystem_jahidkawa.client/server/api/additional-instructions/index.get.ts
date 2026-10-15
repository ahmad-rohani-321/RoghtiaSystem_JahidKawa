import type { AdditionalInstruction } from '../../../shared/types/treatment'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  return (await requestOwnedApi<AdditionalInstruction[]>(event, '/api/additional-instructions'))._data
})
