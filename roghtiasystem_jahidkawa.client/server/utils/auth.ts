import { createError, deleteCookie, getCookie, getHeader, getRequestURL, setCookie, setHeader, type H3Event } from 'h3'
import type { AuthCredentials, AuthTokenResponse } from '../../shared/types/auth'

export const AUTH_COOKIE = 'roghtia_auth'

export function preventAuthCaching(event: H3Event) {
  setHeader(event, 'Cache-Control', 'no-store')
  setHeader(event, 'Vary', 'Cookie')
}

export function assertAuthMutation(event: H3Event) {
  preventAuthCaching(event)
  // A custom header requires cross-origin clients to pass a CORS preflight.
  const origin = getHeader(event, 'origin')
  if (getHeader(event, 'x-roghtia-request') !== '1' || (origin && origin !== getRequestURL(event).origin)) {
    throw createError({ statusCode: 403, data: { code: 'INVALID_ORIGIN' } })
  }
}

export function readAuthCredentials(value: unknown, register = false): AuthCredentials {
  const body = value as Partial<AuthCredentials> | null
  if (!body || typeof body.userName !== 'string' || typeof body.password !== 'string' ||
      !body.userName.trim() || body.userName.trim().length > 64 ||
      body.password.length < (register ? 8 : 1) || body.password.length > 128 ||
      (body.rememberMe !== undefined && typeof body.rememberMe !== 'boolean')) {
    throw createError({ statusCode: 400, data: { code: 'VALIDATION_ERROR' } })
  }
  return { userName: body.userName.trim(), password: body.password, rememberMe: register ? false : body.rememberMe === true }
}

export async function requestAuthApi<T>(event: H3Event, path: 'login' | 'register' | 'me', options: { body?: AuthCredentials, token?: string } = {}) {
  preventAuthCaching(event)
  const config = useRuntimeConfig(event)
  try {
    return await $fetch<T>(`/api/auth/${path}`, {
      baseURL: config.authApiBase,
      method: options.body ? 'POST' : 'GET',
      body: options.body,
      headers: options.token ? { Authorization: `Bearer ${options.token}` } : undefined,
      redirect: 'error',
      timeout: 10000,
      retry: 0
    })
  } catch (error: unknown) {
    const failure = error as { statusCode?: number, data?: { code?: string } }
    const status = failure.statusCode ?? 503
    if (path === 'me' && status === 401) clearAuthCookie(event)
    const publicStatus = [400, 401, 403, 409, 429].includes(status) ? status : 503
    const codes: Record<number, string> = { 400: 'VALIDATION_ERROR', 401: path === 'me' ? 'UNAUTHENTICATED' : 'INVALID_CREDENTIALS', 403: 'FORBIDDEN', 409: 'USERNAME_TAKEN', 429: 'TOO_MANY_REQUESTS', 503: 'SERVICE_UNAVAILABLE' }
    throw createError({ statusCode: publicStatus, data: { code: codes[publicStatus] } })
  }
}

function authCookieOptions(event: H3Event) {
  const secure = useRuntimeConfig(event).authCookieSecure
  return { httpOnly: true, secure: secure !== false && String(secure) !== 'false', sameSite: 'strict' as const, path: '/' }
}

export function establishAuthSession(event: H3Event, response: AuthTokenResponse, rememberMe: boolean) {
  const expiresAt = Date.parse(response?.expiresAt)
  if (!response?.token || !response.user?.userName || !Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
    throw createError({ statusCode: 502, data: { code: 'INVALID_AUTH_RESPONSE' } })
  }
  // Replaces any prior session; only opted-in sessions get persistent attributes.
  setCookie(event, AUTH_COOKIE, response.token, {
    ...authCookieOptions(event),
    ...(rememberMe ? { maxAge: Math.max(1, Math.floor((expiresAt - Date.now()) / 1000)), expires: new Date(expiresAt) } : {})
  })
  // The JWT stays server-side and never enters the Nuxt payload or browser storage.
  return { user: response.user, expiresAt: response.expiresAt }
}

export function requireAuthToken(event: H3Event) {
  preventAuthCaching(event)
  const token = getCookie(event, AUTH_COOKIE)
  if (!token) throw createError({ statusCode: 401, data: { code: 'UNAUTHENTICATED' } })
  return token
}

export function clearAuthCookie(event: H3Event) {
  deleteCookie(event, AUTH_COOKIE, authCookieOptions(event))
}

export function getVerifiedTokenExpiry(token: string) {
  // Call only after the API has verified the signature, issuer, audience and expiry.
  try {
    const claims = JSON.parse(Buffer.from(token.split('.')[1]!, 'base64url').toString('utf8'))
    if (typeof claims.exp !== 'number') throw new Error('Missing expiry')
    return new Date(claims.exp * 1000).toISOString()
  } catch {
    throw createError({ statusCode: 401, data: { code: 'UNAUTHENTICATED' } })
  }
}
