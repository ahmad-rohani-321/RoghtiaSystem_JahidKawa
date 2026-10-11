import { createError, getHeader, getQuery, getRouterParam, type H3Event } from 'h3'
import type { TreatmentInput } from '../../shared/types/treatment'

const invalid = () => createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })

export function readTreatmentId(event: H3Event) {
  const value = getRouterParam(event, 'id') || ''
  const id = Number(value)
  if (!/^\d+$/.test(value) || !Number.isSafeInteger(id) || id < 1 || id > 2147483647) {
    throw createError({ statusCode: 404, data: { code: 'NOT_FOUND' } })
  }
  return id
}

export function readTreatmentQuery(event: H3Event) {
  const query = getQuery(event)
  function integer(value: unknown, fallback: number, max: number) {
    if (value === undefined) return fallback
    if (typeof value !== 'string' || !/^\d+$/.test(value)) throw invalid()
    const number = Number(value)
    if (!Number.isSafeInteger(number) || number < 1 || number > max) throw invalid()
    return number
  }
  const patientName = query.patientName ?? ''
  if (typeof patientName !== 'string' || patientName.length > 200) throw invalid()
  const fromUtc = query.fromUtc
  const toUtc = query.toUtc
  if ((fromUtc === undefined) !== (toUtc === undefined)) throw invalid()
  if (fromUtc !== undefined && (typeof fromUtc !== 'string' || typeof toUtc !== 'string' ||
      !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(fromUtc) ||
      !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(toUtc) ||
      !Number.isFinite(Date.parse(fromUtc)) || !Number.isFinite(Date.parse(toUtc)) ||
      Date.parse(fromUtc) >= Date.parse(toUtc) || Date.parse(toUtc) - Date.parse(fromUtc) > 32 * 86400000)) throw invalid()
  if (query.hasPrescription !== undefined && query.hasPrescription !== 'true' && query.hasPrescription !== 'false') throw invalid()
  return {
    page: integer(query.page, 1, 2147483647), pageSize: integer(query.pageSize, 20, 100),
    patientName: patientName.trim(),
    ...(fromUtc === undefined ? {} : { fromUtc: fromUtc as string, toUtc: toUtc as string }),
    hasPrescription: query.hasPrescription === 'true'
  }
}

export async function readTreatmentInput(event: H3Event): Promise<TreatmentInput> {
  assertAuthMutation(event)
  requireAuthToken(event)
  if (getHeader(event, 'content-type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') throw invalid()
  const raw = await readLimitedBody(event, 32 * 1024, 'VALIDATION_ERROR')
  let value: Partial<TreatmentInput> | null
  try { value = JSON.parse(raw.toString('utf8')) } catch { throw invalid() }
  if (!value || !Number.isInteger(value.patientId) || value.patientId! < 1 || value.patientId! > 2147483647 ||
      typeof value.bloodPressure !== 'string' || !/^(?:|[1-9][0-9]{1,2}\/[1-9][0-9]{1,2})$/.test(value.bloodPressure.trim()) ||
      !Array.isArray(value.clinicalNoteIds) || value.clinicalNoteIds.length > 20 ||
      value.clinicalNoteIds.some(id => !Number.isInteger(id) || id < 1 || id > 2147483647) ||
      new Set(value.clinicalNoteIds).size !== value.clinicalNoteIds.length ||
      !Array.isArray(value.prescriptionItems) || value.prescriptionItems.length > 30 ||
      value.prescriptionItems.some(item => !item || !Number.isInteger(item.medicationId) || item.medicationId < 1 || item.medicationId > 2147483647 ||
        typeof item.duration !== 'string' || !item.duration.trim() || item.duration.trim().length > 100 ||
        !Number.isInteger(item.refills) || item.refills < 0 || item.refills > 100)) throw invalid()
  return {
    patientId: value.patientId!, bloodPressure: value.bloodPressure.trim(), clinicalNoteIds: [...value.clinicalNoteIds],
    prescriptionItems: value.prescriptionItems.map(item => ({ medicationId: item.medicationId, duration: item.duration.trim(), refills: item.refills }))
  }
}

export async function readClinicalNoteInput(event: H3Event) {
  assertAuthMutation(event)
  requireAuthToken(event)
  if (getHeader(event, 'content-type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') throw invalid()
  const raw = await readLimitedBody(event, 2048, 'VALIDATION_ERROR')
  let value: { text?: unknown } | null
  try { value = JSON.parse(raw.toString('utf8')) } catch { throw invalid() }
  if (!value || typeof value.text !== 'string' || !value.text.trim() || value.text.trim().length > 300) throw invalid()
  return { text: value.text.trim() }
}
