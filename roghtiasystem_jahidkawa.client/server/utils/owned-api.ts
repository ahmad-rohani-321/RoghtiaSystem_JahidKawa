import { createError, getHeader, getRequestWebStream, type H3Event } from 'h3'

// Bound uploads before buffering, including requests without Content-Length.
export async function readLimitedBody(event: H3Event, limit: number, errorCode = 'IMAGE_TOO_LARGE') {
  if (Number(getHeader(event, 'content-length')) > limit) {
    throw createError({ statusCode: 413, data: { code: errorCode } })
  }
  const reader = getRequestWebStream(event)?.getReader()
  if (!reader) return Buffer.alloc(0)
  const chunks: Uint8Array[] = []
  let size = 0
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      size += value.byteLength
      if (size > limit) {
        await reader.cancel()
        throw createError({ statusCode: 413, data: { code: errorCode } })
      }
      chunks.push(value)
    }
    return Buffer.concat(chunks)
  } finally {
    reader.releaseLock()
  }
}

export async function requestOwnedApi<T>(event: H3Event, path: string, options: {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: Buffer | Record<string, string | number>
  query?: Record<string, string | number>
  contentType?: string
  binary?: boolean
} = {}) {
  const token = requireAuthToken(event)
  try {
    return await $fetch.raw<T>(path, {
      baseURL: useRuntimeConfig(event).authApiBase,
      method: options.method || 'GET',
      body: options.body,
      query: options.query,
      headers: { Authorization: `Bearer ${token}`, ...(options.contentType ? { 'Content-Type': options.contentType } : {}) },
      ...(options.binary ? { responseType: 'arrayBuffer' as const } : {}),
      redirect: 'error', timeout: 15000, retry: 0
    })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, data?: { code?: string } }
    const status = [400, 401, 403, 404, 409, 413, 415, 429].includes(failure.statusCode || 0) ? failure.statusCode! : 503
    if (status === 401) clearAuthCookie(event)
    const allowedCodes = ['VALIDATION_ERROR', 'UNAUTHENTICATED', 'FORBIDDEN', 'NOT_FOUND', 'DOCTOR_INFORMATION_EXISTS', 'INVALID_IMAGE', 'IMAGE_TOO_LARGE', 'PASSWORD_INCORRECT', 'TOO_MANY_REQUESTS']
    const fallback: Record<number, string> = { 400: 'VALIDATION_ERROR', 401: 'UNAUTHENTICATED', 403: 'FORBIDDEN', 404: 'NOT_FOUND', 409: 'DOCTOR_INFORMATION_EXISTS', 413: 'IMAGE_TOO_LARGE', 415: 'INVALID_IMAGE', 429: 'TOO_MANY_REQUESTS', 503: 'SERVICE_UNAVAILABLE' }
    throw createError({ statusCode: status, data: { code: allowedCodes.includes(failure.data?.code || '') ? failure.data!.code : fallback[status] } })
  }
}

export async function saveDoctorInformation(event: H3Event, method: 'POST' | 'PUT') {
  assertAuthMutation(event)
  requireAuthToken(event)
  const contentType = getHeader(event, 'content-type') || ''
  if (!contentType.toLowerCase().startsWith('multipart/form-data;')) {
    throw createError({ statusCode: 415, data: { code: 'VALIDATION_ERROR' } })
  }
  const body = await readLimitedBody(event, 7 * 1024 * 1024)
  return (await requestOwnedApi(event, '/api/doctor-information', { method, body, contentType }))._data
}
