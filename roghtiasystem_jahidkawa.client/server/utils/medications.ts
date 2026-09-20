import { createError, getHeader, getQuery, getRouterParam, type H3Event } from 'h3'
import type { MedicationInput } from '../../shared/types/medication'

function invalidMedication() {
  return createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
}

export function readMedicationId(event: H3Event) {
  const value = getRouterParam(event, 'id') || ''
  const id = Number(value)
  if (!/^\d+$/.test(value) || !Number.isSafeInteger(id) || id < 1 || id > 2147483647) {
    throw createError({ statusCode: 404, data: { code: 'NOT_FOUND' } })
  }
  return id
}

export function readMedicationQuery(event: H3Event) {
  const query = getQuery(event)
  function integer(value: unknown, fallback: number, max: number) {
    if (value === undefined) return fallback
    if (typeof value !== 'string' || !/^\d+$/.test(value)) throw invalidMedication()
    const number = Number(value)
    if (!Number.isSafeInteger(number) || number < 1 || number > max) throw invalidMedication()
    return number
  }
  const search = query.search ?? ''
  if (typeof search !== 'string' || search.length > 200) throw invalidMedication()
  // Only pagination and search are forwarded; ownership is resolved by the API.
  return { page: integer(query.page, 1, 2147483647), pageSize: integer(query.pageSize, 20, 100), search: search.trim() }
}

export async function readMedicationInput(event: H3Event): Promise<MedicationInput> {
  assertAuthMutation(event)
  requireAuthToken(event)
  if (getHeader(event, 'content-type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') throw invalidMedication()
  const raw = await readLimitedBody(event, 16384, 'VALIDATION_ERROR')
  let value: Partial<MedicationInput> | null
  try { value = JSON.parse(raw.toString('utf8')) }
  catch { throw invalidMedication() }
  if (!value || typeof value.name !== 'string' || !value.name.trim() || value.name.length > 200 ||
      typeof value.type !== 'string' || !value.type.trim() || value.type.length > 100 ||
      typeof value.quantity !== 'number' || !Number.isInteger(value.quantity) || value.quantity < 0 || value.quantity > 2147483647 ||
      (value.remarks != null && (typeof value.remarks !== 'string' || value.remarks.length > 1000))) {
    throw invalidMedication()
  }
  // Never forward client-supplied IDs or users, including extra JSON properties.
  return { name: value.name.trim(), type: value.type.trim(), quantity: value.quantity, remarks: value.remarks?.trim() || '' }
}
