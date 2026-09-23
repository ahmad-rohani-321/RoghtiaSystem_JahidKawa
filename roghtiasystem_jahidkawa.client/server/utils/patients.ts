import { createError, getHeader, getQuery, getRouterParam, type H3Event } from 'h3'
import type { PatientInput } from '../../shared/types/patient'

function invalidPatient() {
  return createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
}

export function readPatientId(event: H3Event) {
  const value = getRouterParam(event, 'id') || ''
  const id = Number(value)
  if (!/^\d+$/.test(value) || !Number.isSafeInteger(id) || id < 1 || id > 2147483647) {
    throw createError({ statusCode: 404, data: { code: 'NOT_FOUND' } })
  }
  return id
}

export function readPatientQuery(event: H3Event) {
  const query = getQuery(event)
  function integer(value: unknown, fallback: number, max: number) {
    if (value === undefined) return fallback
    if (typeof value !== 'string' || !/^\d+$/.test(value)) throw invalidPatient()
    const number = Number(value)
    if (!Number.isSafeInteger(number) || number < 1 || number > max) throw invalidPatient()
    return number
  }
  const search = query.search ?? ''
  if (typeof search !== 'string' || search.length > 200) throw invalidPatient()
  // Only pagination and search are forwarded; ownership is resolved by the API.
  return { page: integer(query.page, 1, 2147483647), pageSize: integer(query.pageSize, 20, 100), search: search.trim() }
}

export async function readPatientInput(event: H3Event): Promise<PatientInput> {
  assertAuthMutation(event)
  requireAuthToken(event)
  if (getHeader(event, 'content-type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') throw invalidPatient()
  const raw = await readLimitedBody(event, 16384, 'VALIDATION_ERROR')
  let value: Partial<PatientInput> | null
  try { value = JSON.parse(raw.toString('utf8')) }
  catch { throw invalidPatient() }
  if (!value || typeof value.name !== 'string' || !value.name.trim() || value.name.length > 200 ||
      (value.phone != null && (typeof value.phone !== 'string' || value.phone.length > 32)) ||
      typeof value.gender !== 'string' || !['Male', 'Female', 'Other'].includes(value.gender) ||
      typeof value.age !== 'number' || !Number.isInteger(value.age) || value.age < 0 || value.age > 150 ||
      (value.address != null && (typeof value.address !== 'string' || value.address.length > 1000))) {
    throw invalidPatient()
  }
  // Never forward client-supplied IDs or users, including extra JSON properties.
  return { name: value.name.trim(), phone: value.phone?.trim() || '', age: value.age, gender: value.gender, address: value.address?.trim() || '' }
}
