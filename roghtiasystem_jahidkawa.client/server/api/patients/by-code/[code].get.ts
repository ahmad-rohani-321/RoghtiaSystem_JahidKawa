import { createError, getRouterParam } from 'h3'
import type { Patient } from '../../../../shared/types/patient'

export default defineEventHandler(async (event) => {
  requireAuthToken(event)
  const code = getRouterParam(event, 'code') || ''
  if (!/^RT-[1-9][0-9]*$/.test(code) || code.length > 32) {
    throw createError({ statusCode: 404, data: { code: 'NOT_FOUND' } })
  }
  return (await requestOwnedApi<Patient>(event, `/api/patients/by-code/${encodeURIComponent(code)}`))._data
})
