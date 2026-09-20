import assert from 'node:assert/strict'
import { before, after, test } from 'node:test'
import { spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { createHmac, randomBytes } from 'node:crypto'
import { mkdtemp, rm, mkdir } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, resolve, basename, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'
import { DatabaseSync } from 'node:sqlite'

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const serverRoot = resolve(clientRoot, '../RoghtiaSystem_JahidKawa.Server')
const serverDll = resolve(serverRoot, 'bin/Debug/net10.0/RoghtiaSystem_JahidKawa.Server.dll')
const processes = []
const signingKey = randomBytes(64).toString('base64')
const password = 'پټنوم-' + randomBytes(12).toString('hex')
let temporaryRoot, api, web, secureWeb, apiProcess, apiPort, persistentCookie

async function freePort() {
  const server = createServer()
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
  const port = server.address().port
  await new Promise(resolve => server.close(resolve))
  return port
}

async function start(command, args, cwd, env, readyUrl) {
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] })
  processes.push(child)
  let output = ''
  child.stdout.on('data', data => { output = (output + data).slice(-10000) })
  child.stderr.on('data', data => { output = (output + data).slice(-10000) })
  child.getTestOutput = () => output
  let startError
  child.on('error', error => { startError = error })
  for (let attempt = 0; attempt < 160; attempt++) {
    if (startError) throw startError
    if (child.exitCode !== null) throw new Error(`Test server exited: ${output}`)
    try { if ((await fetch(readyUrl, { redirect: 'manual' })).status < 500) return child } catch {}
    await delay(250)
  }
  throw new Error(`Test server failed to start: ${output}`)
}

async function stop(child) {
  if (child.exitCode !== null || child.signalCode) return
  const exited = new Promise(resolve => child.once('exit', resolve))
  child.kill()
  await exited
}

function apiEnvironment(database, extras = {}) {
  return {
    ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_HOSTINGSTARTUPASSEMBLIES: '',
  Auth__SigningKey: signingKey, Auth__RateLimitPermitLimit: '200', Auth__SessionHours: '24', Auth__RememberDays: '30',
    ConnectionStrings__MainDatabase: `Data Source=${database};Password=${signingKey}`,
    Logging__LogLevel__Default: 'Warning', Logging__EventLog__LogLevel__Default: 'None', ...extras
  }
}

async function startApi(port, database, extras = {}) {
  return start('dotnet', [serverDll, '--urls', `http://127.0.0.1:${port}`], serverRoot,
    apiEnvironment(database, extras), `http://127.0.0.1:${port}/api/health`)
}

async function startWeb(secure) {
  const port = await freePort()
  const origin = `http://127.0.0.1:${port}`
  await start(process.execPath, ['.output/server/index.mjs'], clientRoot,
    { PORT: String(port), HOST: '127.0.0.1', NUXT_AUTH_API_BASE: api, NUXT_AUTH_COOKIE_SECURE: String(secure) }, `${origin}/login`)
  return origin
}

async function post(origin, path, data, options = {}) {
  return fetch(origin + path, {
    method: 'POST', redirect: 'manual',
    headers: { 'content-type': 'application/json', ...(origin !== api ? { 'x-roghtia-request': '1', origin } : {}), ...options.headers },
    body: JSON.stringify(data)
  })
}

const authCookie = response => response.headers.get('set-cookie')?.split(';')[0]
function signedToken(claims = {}) {
  const now = Math.floor(Date.now() / 1000)
  const head = Buffer.from(JSON.stringify({ alg: 'HS512', typ: 'JWT' })).toString('base64url')
  const payload = Buffer.from(JSON.stringify({ sub: '1', unique_name: 'test-user', iss: 'roghtia-api', aud: 'roghtia-client', nbf: now - 10, exp: now + 600, ...claims })).toString('base64url')
  const signature = createHmac('sha512', signingKey).update(`${head}.${payload}`).digest('base64url')
  return `${head}.${payload}.${signature}`
}

const doctorDefaults = {
  doctorNameEnglish: '', doctorNamePashto: '', doctorProfessionPashto: '',
  doctorProfessionEnglish: '', hospitalNamePashto: '',
  doctorPhoto: null, doctorLogo: null, hospitalLogo: null
}
const doctorFields = {
  doctorNameEnglish: 'Dr. Ahmad', doctorNamePashto: 'ډاکټر احمد',
  doctorProfessionPashto: 'د داخله ناروغیو متخصص', doctorProfessionEnglish: 'Internal medicine',
  hospitalNamePashto: 'روغتیا روغتون'
}
const tinyPng = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRCsAAAAASUVORK5CYII=', 'base64')

async function account(label, rememberMe = false) {
  const userName = `${label}-${randomBytes(5).toString('hex')}`
  const registered = await post(api, '/api/auth/register', { userName, password })
  assert.equal(registered.status, 200)
  const { user, token } = await registered.json()
  const login = await post(web, '/api/auth/login', { userName, password, rememberMe })
  assert.equal(login.status, 200)
  return { user, token, cookie: authCookie(login) }
}

function doctorForm(values = doctorFields, files = {}) {
  const form = new FormData()
  for (const [name, value] of Object.entries(values)) form.set(name, String(value))
  for (const [name, file] of Object.entries(files)) {
    form.set(name, new Blob([file.bytes ?? tinyPng], { type: file.type ?? 'image/png' }), file.name ?? 'doctor.png')
  }
  return form
}

async function doctorRequest(origin, session, method = 'GET', options = {}) {
  const headers = origin === api ? { authorization: `Bearer ${session.token}` } : { cookie: session.cookie }
  if (method !== 'GET' && origin !== api) Object.assign(headers, { 'x-roghtia-request': '1', origin })
  return fetch(origin + (options.path ?? '/api/doctor-information'), {
    method, redirect: 'manual', headers: { ...headers, ...options.headers }, body: options.body
  })
}

async function medicationRequest(origin, session, method = 'GET', options = {}) {
  const headers = origin === api ? { authorization: `Bearer ${session.token}` } : { cookie: session.cookie }
  if (method !== 'GET' && origin !== api) Object.assign(headers, { 'x-roghtia-request': '1', origin })
  if (options.body !== undefined) headers['content-type'] = 'application/json'
  return fetch(origin + (options.path ?? '/api/medications'), {
    method, redirect: 'manual', headers: { ...headers, ...options.headers },
    body: options.body === undefined ? undefined : JSON.stringify(options.body)
  })
}

function assertMedication(value, owner, fields) {
  assert.deepEqual(Object.keys(value).sort(), ['id', 'name', 'quantity', 'remarks', 'type', 'user'])
  assert.ok(Number.isInteger(value.id) && value.id > 0)
  assert.deepEqual(value.user, { id: owner.user.id, userName: owner.user.userName })
  for (const [key, expected] of Object.entries(fields)) assert.equal(value[key], expected, key)
}

function assertDoctorDefaults(value) {
  assert.equal(value.exists, false)
  for (const [key, expected] of Object.entries(doctorDefaults)) assert.equal(value[key], expected, key)
}

before(async () => {
  temporaryRoot = await mkdtemp(join(tmpdir(), 'roghtia-auth-tests-'))
  apiPort = await freePort()
  api = `http://127.0.0.1:${apiPort}`
  apiProcess = await startApi(apiPort, join(temporaryRoot, 'users.db'))
  web = await startWeb(false)
  secureWeb = await startWeb(true)
}, { timeout: 120000 })

after(async () => {
  for (const child of processes.reverse()) await stop(child)
  if (temporaryRoot && dirname(resolve(temporaryRoot)) === resolve(tmpdir()) && basename(temporaryRoot).startsWith('roghtia-auth-tests-')) {
    await rm(temporaryRoot, { recursive: true, force: true })
  }
})

test('protected pages redirect to login; public forms remain standalone', async () => {
  for (const path of ['/', '/patients', '/prescriptions', '/medications', '/reports', '/settings']) {
    const response = await fetch(web + path, { redirect: 'manual' })
    assert.equal(response.status, 302, path)
    assert.match(response.headers.get('location'), /^\/login\?redirect=/)
  }
  for (const path of ['/login', '/register']) {
    const response = await fetch(web + path)
    assert.equal(response.status, 200)
    const html = await response.text()
    assert.ok(!html.includes('bottom-nav'))
    assert.ok(!html.includes('class="topbar"'))
    assert.ok(html.includes(path === '/login' ? 'current-password' : 'new-password'))
    assert.equal(html.includes('ما په یاد وساتئ'), path === '/login')
  }
})

test('registration creates a user, grants no elevated role, and keeps JWT out of frontend response', async () => {
  const response = await post(web, '/api/auth/register', { userName: '  test-user  ', password, role: 'Admin', rememberMe: true })
  assert.equal(response.status, 200)
  const data = await response.json()
  assert.equal(data.user.userName, 'test-user')
  assert.deepEqual(data.user.roles, ['User'])
  assert.deepEqual(data.user.permissions, [])
  assert.equal('token' in data, false)
  const cookie = response.headers.get('set-cookie')
  assert.match(cookie, /HttpOnly/i)
  assert.match(cookie, /SameSite=Strict/i)
  assert.doesNotMatch(cookie, /Max-Age|Expires=/i)
  assert.equal(response.headers.get('cache-control'), 'no-store')
  const me = await fetch(web + '/api/auth/me', { headers: { cookie: authCookie(response) } })
  assert.equal(me.status, 200)
  assert.equal((await me.json()).user.userName, 'test-user')
})

test('server prevents duplicate, case-equivalent and Unicode-equivalent usernames', async (t) => {
  for (const userName of ['test-user', ' TEST-USER ']) {
    const response = await post(api, '/api/auth/register', { userName, password })
    assert.equal(response.status, 409)
    assert.equal((await response.json()).code, 'USERNAME_TAKEN')
  }
  t.diagnostic('Case-equivalent duplicates rejected')
  assert.equal((await post(api, '/api/auth/register', { userName: 'Caf\u00e9', password })).status, 200)
  assert.equal((await post(api, '/api/auth/register', { userName: 'Cafe\u0301', password })).status, 409)
  t.diagnostic('Unicode-equivalent duplicates rejected')
  const duplicates = await Promise.all(Array.from({ length: 4 }, () => post(api, '/api/auth/register', { userName: 'same-concurrent-user', password }))).catch(error => {
    t.diagnostic(apiProcess.getTestOutput())
    throw error
  })
  assert.deepEqual(duplicates.map(r => r.status).sort(), [200, 409, 409, 409])
})

test('login validates Unicode passwords and returns generic errors for invalid credentials', async () => {
  for (const body of [{ userName: 'missing', password }, { userName: 'test-user', password: 'incorrect-password' }, { userName: 'test-user', password: password.replace('پټنوم', 'نویپټ') }]) {
    const response = await post(web, '/api/auth/login', body)
    assert.equal(response.status, 401)
    assert.equal((await response.json()).data.code, 'INVALID_CREDENTIALS')
    assert.equal(response.headers.get('set-cookie'), null)
  }
  assert.equal((await fetch(api + '/api/auth/login', { headers: { authorization: `Bearer ${signedToken()}` } })).status, 405)
})

test('Remember Me persists, opting out replaces it with a session cookie', async () => {
  const persistent = await post(web, '/api/auth/login', { userName: 'TEST-USER', password, rememberMe: true })
  assert.equal(persistent.status, 200)
  assert.match(persistent.headers.get('set-cookie'), /Max-Age=\d+/i)
  assert.match(persistent.headers.get('set-cookie'), /Expires=/i)
  persistentCookie = authCookie(persistent)
  const remembered = await persistent.json()
  assert.ok(Date.parse(remembered.expiresAt) - Date.now() > 29 * 86400000)
  const session = await post(web, '/api/auth/login', { userName: 'test-user', password, rememberMe: false }, { headers: { cookie: persistentCookie } })
  assert.equal(session.status, 200)
  assert.doesNotMatch(session.headers.get('set-cookie'), /Max-Age|Expires=/i)
  assert.equal(authCookie(session).split('=')[0], persistentCookie.split('=')[0])
  const lifetime = Date.parse((await session.json()).expiresAt) - Date.now()
  assert.ok(lifetime > 23 * 3600000 && lifetime <= 86460000, `Session token lifetime: ${lifetime}ms`)
  // A fresh client has no session cookie after the browser ends that session.
  assert.equal((await fetch(web + '/api/auth/me')).status, 401)
})

test('secure deployments set Secure; logout expires the HttpOnly cookie', async () => {
  const login = await post(secureWeb, '/api/auth/login', { userName: 'test-user', password, rememberMe: true })
  assert.equal(login.status, 200)
  assert.match(login.headers.get('set-cookie'), /; Secure/i)
  const logout = await post(web, '/api/auth/logout', {}, { headers: { cookie: persistentCookie } })
  assert.equal(logout.status, 200)
  assert.match(logout.headers.get('set-cookie'), /Max-Age=0/i)
  assert.match(logout.headers.get('set-cookie'), /HttpOnly/i)
})

test('API rejects forged, expired and wrong issuer/audience tokens', async () => {
  for (const token of [signedToken({ exp: 1 }), signedToken({ iss: 'other' }), signedToken({ aud: 'other' }), signedToken().slice(0, -8) + 'tampered']) {
    const response = await fetch(api + '/api/auth/me', { headers: { authorization: `Bearer ${token}` } })
    assert.equal(response.status, 401)
  }
  const expired = await fetch(web + '/api/auth/me', { headers: { cookie: `roghtia_auth=${signedToken({ exp: 1 })}` } })
  assert.equal(expired.status, 401)
  assert.match(expired.headers.get('set-cookie'), /Max-Age=0/i)
  const expiredPage = await fetch(web + '/reports', { redirect: 'manual', headers: { cookie: `roghtia_auth=${signedToken({ exp: 1 })}` } })
  assert.equal(expiredPage.status, 302)
  assert.match(expiredPage.headers.get('set-cookie'), /Max-Age=0/i)
})

test('mutations reject cross-site requests and malformed credentials', async () => {
  const noHeader = await fetch(web + '/api/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: '{}' })
  assert.equal(noHeader.status, 403)
  assert.equal((await post(web, '/api/auth/login', { userName: 'test-user', password }, { headers: { origin: 'https://untrusted.example' } })).status, 403)
  for (const body of [{}, { userName: ' ', password }, { userName: 'test-user', password, rememberMe: 'yes' }]) {
    assert.equal((await post(web, '/api/auth/login', body)).status, 400)
  }
  assert.equal((await post(api, '/api/auth/register', { userName: 'short-password', password: 'short' })).status, 400)
})

test('authenticated SSR uses actual identity without leaking JWT and marks Reports active', async () => {
  const response = await fetch(web + '/reports', { headers: { cookie: persistentCookie } })
  assert.equal(response.status, 200)
  const html = await response.text()
  assert.ok(html.includes('test-user'))
  assert.ok(!html.includes(persistentCookie.split('=')[1]))
  assert.equal(response.headers.get('cache-control'), 'no-store')
  const reportLink = html.match(/<a\b[^>]*>/g).find(tag => tag.includes('href="/reports"'))
  assert.ok(reportLink.includes('aria-current="page"'))
})

test('persistent session survives API restart with the same configured key and database', async () => {
  const created = await medicationRequest(web, { cookie: persistentCookie }, 'POST', {
    body: { name: 'Persistent medicine', type: 'Tablet', quantity: 12, remarks: 'Survives restart' }
  })
  assert.equal(created.status, 201)
  const medication = await created.json()
  await stop(apiProcess)
  apiProcess = await startApi(apiPort, join(temporaryRoot, 'users.db'))
  const response = await fetch(web + '/api/auth/me', { headers: { cookie: persistentCookie } })
  assert.equal(response.status, 200)
  assert.equal((await response.json()).user.userName, 'test-user')
  const restored = await medicationRequest(web, { cookie: persistentCookie }, 'GET', { path: `/api/medications/${medication.id}` })
  assert.equal(restored.status, 200)
  assert.deepEqual(await restored.json(), medication)
})

test('legacy user schema upgrades without deleting accounts; password rehashes on login', async () => {
  const path = join(temporaryRoot, 'legacy.db')
  const salt = randomBytes(64)
  const legacyPassword = 'Legacy-Password-123!'
  const hash = createHmac('sha512', salt).update(legacyPassword, 'ascii').digest()
  const db = new DatabaseSync(path)
  db.exec('CREATE TABLE Users (Id INTEGER PRIMARY KEY, UserName TEXT, PasswordHash BLOB, PasswordSalt BLOB)')
  db.prepare('INSERT INTO Users VALUES (1, ?, ?, ?)').run('LegacyUser', hash, salt)
  db.close()
  const port = await freePort()
  const child = await startApi(port, path, { ConnectionStrings__MainDatabase: `Data Source=${path}` })
  const response = await post(`http://127.0.0.1:${port}`, '/api/auth/login', { userName: 'legacyuser', password: legacyPassword })
  assert.equal(response.status, 200)
  const { token } = await response.json()
  const information = await fetch(`http://127.0.0.1:${port}/api/doctor-information`, { headers: { authorization: `Bearer ${token}` } })
  assert.equal(information.status, 200, 'Doctor information schema is added to an existing database')
  assertDoctorDefaults(await information.json())
  const medications = await fetch(`http://127.0.0.1:${port}/api/medications`, { headers: { authorization: `Bearer ${token}` } })
  assert.equal(medications.status, 200, 'Medication schema is added to an existing database')
  assert.deepEqual((await medications.json()).items, [])
  const medicine = await post(`http://127.0.0.1:${port}`, '/api/medications', {
    name: 'Legacy account medicine', type: 'Tablet', quantity: 3, remarks: ''
  }, { headers: { authorization: `Bearer ${token}` } })
  assert.equal(medicine.status, 201)
  assert.equal((await medicine.json()).user.id, 1)
  await stop(child)
  const upgraded = new DatabaseSync(path)
  const user = upgraded.prepare('SELECT * FROM Users').get()
  assert.equal(user.UserName, 'LegacyUser')
  assert.equal(user.NormalizedUserName, 'LEGACYUSER')
  assert.equal(user.PasswordVersion, 1)
  assert.equal(user.TokenVersion, 0)
  assert.notDeepEqual(Buffer.from(user.PasswordHash), hash)
  assert.equal(upgraded.prepare('SELECT COUNT(*) AS count FROM Users').get().count, 1)
  assert.equal(upgraded.prepare('SELECT COUNT(*) AS count FROM Medications').get().count, 1)
  upgraded.close()
})

test('login and registration are rate limited', async () => {
  const port = await freePort()
  const child = await startApi(port, join(temporaryRoot, 'limited.db'), { Auth__RateLimitPermitLimit: '2' })
  const origin = `http://127.0.0.1:${port}`
  assert.equal((await post(origin, '/api/auth/login', { userName: 'missing', password })).status, 401)
  assert.equal((await post(origin, '/api/auth/login', { userName: 'missing', password })).status, 401)
  assert.equal((await post(origin, '/api/auth/register', { userName: 'new-user', password })).status, 429)
  await stop(child)
})

test('default development secrets preserve authentication across restarts', async () => {
  const contentRoot = join(temporaryRoot, 'development')
  await mkdir(contentRoot)
  const port = await freePort()
  const origin = `http://127.0.0.1:${port}`
  const env = apiEnvironment('', { Auth__SigningKey: '', ConnectionStrings__MainDatabase: '' })
  const launch = () => start('dotnet', [serverDll, '--urls', origin, '--contentRoot', contentRoot], serverRoot, env, origin + '/api/health')
  let child = await launch()
  const registered = await post(origin, '/api/auth/register', { userName: 'development-user', password })
  assert.equal(registered.status, 200)
  const { token } = await registered.json()
  await stop(child)
  child = await launch()
  const response = await fetch(origin + '/api/auth/me', { headers: { authorization: `Bearer ${token}` } })
  assert.equal(response.status, 200)
  assert.equal((await response.json()).userName, 'development-user')
  await stop(child)
})

test('doctor information CRUD, reset and SSR remain scoped to the signed-in user', async () => {
  const owner = await account('doctor-owner')
  const other = await account('doctor-other')
  assertDoctorDefaults(await (await doctorRequest(web, owner)).json())
  assertDoctorDefaults(await (await doctorRequest(api, other)).json())
  const missing = await doctorRequest(api, owner, 'PUT', { body: doctorForm() })
  assert.equal(missing.status, 404)
  assert.equal((await missing.json()).code, 'NOT_FOUND')

  const created = await doctorRequest(web, owner, 'POST', {
    body: doctorForm({ ...doctorFields, loggedInUserId: other.user.id })
  })
  assert.equal(created.status, 200)
  assert.equal(created.headers.get('cache-control'), 'no-store')
  const saved = await created.json()
  assert.equal(saved.exists, true)
  for (const [key, value] of Object.entries(doctorFields)) assert.equal(saved[key], value, key)
  assertDoctorDefaults(await (await doctorRequest(api, other)).json())
  const duplicate = await doctorRequest(api, owner, 'POST', { body: doctorForm() })
  assert.equal(duplicate.status, 409)
  assert.equal((await duplicate.json()).code, 'DOCTOR_INFORMATION_EXISTS')

  const otherName = 'ډاکټر بلال'
  const otherCreated = await doctorRequest(api, other, 'POST', { body: doctorForm({ ...doctorFields, doctorNamePashto: otherName }) })
  assert.equal(otherCreated.status, 200)
  const updatedName = 'ډاکټر احمد خان'
  const updated = await doctorRequest(web, owner, 'PUT', {
    body: doctorForm({ ...doctorFields, doctorNamePashto: updatedName, loggedInUserId: other.user.id })
  })
  assert.equal(updated.status, 200)
  assert.equal((await updated.json()).doctorNamePashto, updatedName)
  const otherWithForeignId = await doctorRequest(api, other, 'GET', { path: `/api/doctor-information?loggedInUserId=${owner.user.id}` })
  assert.equal((await otherWithForeignId.json()).doctorNamePashto, otherName)
  const settings = await fetch(web + '/settings', { headers: { cookie: owner.cookie } })
  assert.equal(settings.status, 200)
  const html = await settings.text()
  assert.ok(html.includes(updatedName), 'Saved Pashto doctor name is present on authenticated Settings SSR')
  assert.ok(html.includes(owner.user.userName), 'The signed-in username remains available')
  assert.ok(!html.includes(owner.token), 'JWT must stay out of the rendered Settings page')

  const reset = await doctorRequest(web, other, 'POST', { path: `/api/doctor-information/reset?loggedInUserId=${owner.user.id}` })
  assert.equal(reset.status, 200)
  assertDoctorDefaults(await reset.json())
  assertDoctorDefaults(await (await doctorRequest(api, other)).json())
  assert.equal((await (await doctorRequest(api, owner)).json()).doctorNamePashto, updatedName)
  assert.equal((await doctorRequest(api, other, 'POST', { body: doctorForm({ ...doctorFields, doctorNamePashto: otherName }) })).status, 200)
  const deleted = await doctorRequest(web, owner, 'DELETE', { path: `/api/doctor-information?loggedInUserId=${other.user.id}` })
  assert.equal(deleted.status, 204)
  assertDoctorDefaults(await (await doctorRequest(api, owner)).json())
  assert.equal((await (await doctorRequest(api, other)).json()).doctorNamePashto, otherName)
  assert.equal((await doctorRequest(api, owner, 'DELETE')).status, 204)
})

test('doctor images are private, validated, removable and saved atomically', async () => {
  const owner = await account('image-owner')
  const other = await account('image-other')
  const created = await doctorRequest(web, owner, 'POST', { body: doctorForm(doctorFields, { doctorPhoto: {} }) })
  assert.equal(created.status, 200)
  const saved = await created.json()
  assert.match(saved.doctorPhoto, /^\/api\/doctor-information\/media\/doctor-photo\?/)
  const photo = await doctorRequest(web, owner, 'GET', { path: saved.doctorPhoto })
  assert.equal(photo.status, 200)
  assert.match(photo.headers.get('content-type'), /^image\/png/)
  assert.equal(photo.headers.get('cache-control'), 'no-store')
  assert.deepEqual(Buffer.from(await photo.arrayBuffer()), tinyPng)
  assert.equal((await doctorRequest(web, other, 'GET', { path: saved.doctorPhoto })).status, 404)
  assert.equal((await doctorRequest(api, other, 'GET', { path: saved.doctorPhoto })).status, 404)
  assert.equal((await fetch(web + saved.doctorPhoto)).status, 401)
  assert.equal((await doctorRequest(api, owner, 'GET', { path: '/api/doctor-information/media/unknown' })).status, 404)

  const updated = await doctorRequest(web, owner, 'PUT', {
    body: doctorForm({ ...doctorFields, removeDoctorPhoto: true }, { doctorLogo: {}, hospitalLogo: {} })
  })
  assert.equal(updated.status, 200)
  const logos = await updated.json()
  assert.equal(logos.doctorPhoto, null)
  assert.equal((await doctorRequest(web, owner, 'GET', { path: saved.doctorPhoto })).status, 404)
  for (const path of [logos.doctorLogo, logos.hospitalLogo]) {
    assert.equal((await doctorRequest(web, owner, 'GET', { path })).status, 200)
    assert.equal((await doctorRequest(web, other, 'GET', { path })).status, 404)
  }

  for (const file of [
    { bytes: '<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>', type: 'image/svg+xml', name: 'logo.svg' },
    { bytes: '<script>not a PNG</script>', type: 'image/png', name: 'logo.png' },
    { bytes: tinyPng, type: 'image/jpeg', name: 'logo.jpg' }
  ]) {
    const invalid = await doctorRequest(web, owner, 'PUT', {
      body: doctorForm({ ...doctorFields, doctorNamePashto: 'دا بدلون باید خوندي نه شي' }, { doctorLogo: file })
    })
    assert.equal(invalid.status, 400)
    assert.equal((await invalid.json()).data.code, 'INVALID_IMAGE')
    const unchanged = await (await doctorRequest(api, owner)).json()
    assert.equal(unchanged.doctorNamePashto, doctorFields.doctorNamePashto)
    assert.equal(unchanged.doctorLogo, logos.doctorLogo)
    assert.equal(unchanged.hospitalLogo, logos.hospitalLogo)
  }
  const large = Buffer.concat([tinyPng, Buffer.alloc(2 * 1024 * 1024)])
  const oversized = await doctorRequest(web, owner, 'PUT', { body: doctorForm(doctorFields, { doctorPhoto: { bytes: large } }) })
  assert.equal(oversized.status, 413)
  assert.equal((await oversized.json()).data.code, 'IMAGE_TOO_LARGE')
  assert.equal((await (await doctorRequest(api, owner)).json()).doctorPhoto, null)
  assert.equal((await doctorRequest(web, owner, 'POST', { path: '/api/doctor-information/reset' })).status, 200)
  for (const path of [logos.doctorLogo, logos.hospitalLogo]) assert.equal((await doctorRequest(web, owner, 'GET', { path })).status, 404)
})

test('doctor and password mutations require authentication and reject cross-site requests', async () => {
  const session = await account('settings-csrf')
  const mutations = [
    ['POST', '/api/doctor-information'], ['PUT', '/api/doctor-information'],
    ['DELETE', '/api/doctor-information'], ['POST', '/api/doctor-information/reset'],
    ['POST', '/api/auth/change-password']
  ]
  assert.equal((await fetch(web + '/api/doctor-information')).status, 401)
  for (const [method, path] of mutations) {
    const passwordChange = path.endsWith('change-password')
    const body = () => passwordChange ? JSON.stringify({ currentPassword: password, newPassword: password + '-new' }) : method === 'POST' || method === 'PUT' ? doctorForm() : undefined
    const headers = passwordChange ? { 'content-type': 'application/json' } : {}
    const unauthenticated = await fetch(web + path, { method, headers: { ...headers, origin: web, 'x-roghtia-request': '1' }, body: body() })
    assert.equal(unauthenticated.status, 401, `${method} ${path} without authentication`)
    const missingCsrf = await fetch(web + path, { method, headers: { ...headers, cookie: session.cookie, origin: web }, body: body() })
    assert.equal(missingCsrf.status, 403, `${method} ${path} without request header`)
    const crossSite = await fetch(web + path, { method, headers: { ...headers, cookie: session.cookie, origin: 'https://untrusted.example', 'x-roghtia-request': '1' }, body: body() })
    assert.equal(crossSite.status, 403, `${method} ${path} with foreign origin`)
  }
  assertDoctorDefaults(await (await doctorRequest(api, session)).json())
  assert.equal((await post(api, '/api/auth/login', { userName: session.user.userName, password })).status, 200)
})

test('password changes validate the current password, target only the current user and revoke existing sessions', async () => {
  const owner = await account('password-owner', true)
  const other = await account('password-other', true)
  const newPassword = 'نوی-پټنوم-' + randomBytes(12).toString('hex')
  const options = { headers: { cookie: owner.cookie } }
  const incorrect = await post(web, '/api/auth/change-password', { currentPassword: 'wrong-password', newPassword }, options)
  assert.equal(incorrect.status, 400)
  assert.equal((await incorrect.json()).data.code, 'PASSWORD_INCORRECT')
  for (const invalid of ['', 'short', 'x'.repeat(129)]) {
    const response = await post(web, '/api/auth/change-password', { currentPassword: password, newPassword: invalid }, options)
    assert.equal(response.status, 400)
  }
  assert.equal((await fetch(web + '/api/auth/me', { headers: { cookie: owner.cookie } })).status, 200)
  const changed = await post(web, '/api/auth/change-password', {
    currentPassword: password, newPassword, loggedInUserId: other.user.id, userId: other.user.id, userName: other.user.userName
  }, options)
  assert.equal(changed.status, 204)
  assert.match(changed.headers.get('set-cookie'), /Max-Age=0/i)
  assert.equal(changed.headers.get('cache-control'), 'no-store')
  const revoked = await fetch(web + '/api/auth/me', { headers: { cookie: owner.cookie } })
  assert.equal(revoked.status, 401)
  assert.match(revoked.headers.get('set-cookie'), /Max-Age=0/i)
  for (const path of ['/api/auth/me', '/api/doctor-information']) {
    assert.equal((await fetch(api + path, { headers: { authorization: `Bearer ${owner.token}` } })).status, 401)
  }
  assert.equal((await post(api, '/api/auth/login', { userName: owner.user.userName, password })).status, 401)
  const login = await post(web, '/api/auth/login', { userName: owner.user.userName, password: newPassword, rememberMe: true })
  assert.equal(login.status, 200)
  assert.match(login.headers.get('set-cookie'), /Max-Age=/i)
  assert.equal((await fetch(web + '/api/auth/me', { headers: { cookie: authCookie(login) } })).status, 200)
  assert.equal((await fetch(web + '/api/auth/me', { headers: { cookie: other.cookie } })).status, 200)
  assert.equal((await post(api, '/api/auth/login', { userName: other.user.userName, password })).status, 200)
  assert.equal((await post(api, '/api/auth/login', { userName: other.user.userName, password: newPassword })).status, 401)
})

test('medication create, read and update enforce ownership and expose no delete operation', async () => {
  const owner = await account('medicine-owner')
  const other = await account('medicine-other')
  const ownRecords = []
  for (const origin of [api, web]) {
    const fields = { name: `Amoxicillin ${origin === api ? 'API' : 'Web'}`, type: 'Capsule', quantity: 24, remarks: 'له خوړو وروسته' }
    const created = await medicationRequest(origin, owner, 'POST', {
      body: { ...fields, name: `  ${fields.name}  `, type: '  Capsule  ', userId: other.user.id, user: { id: other.user.id, userName: other.user.userName } }
    })
    assert.equal(created.status, 201)
    if (origin === web) assert.equal(created.headers.get('cache-control'), 'no-store')
    const saved = await created.json()
    assertMedication(saved, owner, fields)
    ownRecords.push(saved)
    const read = await medicationRequest(origin, owner, 'GET', { path: `/api/medications/${saved.id}` })
    assert.equal(read.status, 200)
    assert.deepEqual(await read.json(), saved)

    for (const id of [saved.id, 2147483647]) {
      assert.equal((await medicationRequest(origin, other, 'GET', { path: `/api/medications/${id}` })).status, 404)
      const forbiddenUpdate = await medicationRequest(origin, other, 'PUT', {
        path: `/api/medications/${id}`, body: { ...fields, name: 'Unauthorized edit', user: { id: owner.user.id }, userId: owner.user.id }
      })
      assert.equal(forbiddenUpdate.status, 404)
    }
    assert.deepEqual(await (await medicationRequest(origin, owner, 'GET', { path: `/api/medications/${saved.id}` })).json(), saved)

    const changedFields = { ...fields, quantity: 0, remarks: 'نوی یادښت' }
    const updated = await medicationRequest(origin, owner, 'PUT', {
      path: `/api/medications/${saved.id}`, body: { ...changedFields, id: 2147483647, userId: other.user.id, user: { id: other.user.id } }
    })
    assert.equal(updated.status, 200)
    const changed = await updated.json()
    assert.equal(changed.id, saved.id, 'Record identity comes from the route')
    assertMedication(changed, owner, changedFields)
    const removed = await medicationRequest(origin, owner, 'DELETE', { path: `/api/medications/${saved.id}` })
    assert.ok([404, 405].includes(removed.status), `DELETE must be unavailable; received ${removed.status}`)
    assert.deepEqual(await (await medicationRequest(origin, owner, 'GET', { path: `/api/medications/${saved.id}` })).json(), changed)
  }
  const otherCreated = await medicationRequest(web, other, 'POST', { body: { name: 'Other user private medicine', type: 'Syrup', quantity: 7 } })
  assert.equal(otherCreated.status, 201)
  const otherMedication = await otherCreated.json()
  assertMedication(otherMedication, other, { name: 'Other user private medicine', type: 'Syrup', quantity: 7, remarks: '' })
  for (const origin of [api, web]) {
    const removedCollection = await medicationRequest(origin, owner, 'DELETE')
    assert.ok([404, 405].includes(removedCollection.status), 'Deleting the medication collection must be unavailable')
    const ownList = await (await medicationRequest(origin, owner, 'GET', { path: `/api/medications?userId=${other.user.id}&User=${other.user.id}` })).json()
    assert.equal(ownList.total, 2)
    assert.deepEqual(ownList.items.map(item => item.id), ownRecords.map(item => item.id).reverse())
    assert.ok(ownList.items.every(item => item.user.id === owner.user.id))
    const otherList = await (await medicationRequest(origin, other, 'GET', { path: `/api/medications?userId=${owner.user.id}` })).json()
    assert.equal(otherList.total, 1)
    assert.deepEqual(otherList.items, [otherMedication])
  }
  const page = await fetch(web + '/medications', { headers: { cookie: owner.cookie } })
  assert.equal(page.status, 200)
  const html = await page.text()
  assert.ok(html.includes('Amoxicillin Web'), 'SSR displays the logged-in user’s medications')
  assert.ok(!html.includes(otherMedication.name), 'SSR cannot expose another user’s medication')
  assert.ok(!html.includes(owner.token), 'Medication page must not expose the authentication token')
})

test('medication search and pagination are stable and restricted to the current user', async () => {
  const owner = await account('medicine-search')
  const other = await account('medicine-search-other')
  const medicines = [
    { name: 'Amoxicillin', type: 'Capsule', quantity: 20, remarks: '' },
    { name: 'Paracetamol', type: 'Tablet', quantity: 30, remarks: 'Amoxicillin follow-up' },
    { name: 'Vitamin C', type: 'Capsule', quantity: 40, remarks: 'د ماشومانو لپاره' },
    { name: 'Saline', type: 'Solution', quantity: 50, remarks: '' }
  ]
  const ids = []
  for (const fields of medicines) {
    const response = await medicationRequest(web, owner, 'POST', { body: fields })
    assert.equal(response.status, 201)
    ids.push((await response.json()).id)
  }
  assert.equal((await medicationRequest(api, other, 'POST', { body: { ...medicines[0], name: 'Amoxicillin private' } })).status, 201)
  for (const origin of [api, web]) {
    const first = await (await medicationRequest(origin, owner, 'GET', { path: '/api/medications?page=1&pageSize=2' })).json()
    const second = await (await medicationRequest(origin, owner, 'GET', { path: '/api/medications?page=2&pageSize=2' })).json()
    assert.deepEqual(Object.keys(first).sort(), ['items', 'page', 'pageSize', 'total'])
    assert.deepEqual([first.total, first.page, first.pageSize, second.total, second.page, second.pageSize], [4, 1, 2, 4, 2, 2])
    assert.deepEqual([...first.items, ...second.items].map(item => item.id), [...ids].reverse())
    for (const [search, matches] of [['aMoXiCiLlIn', [ids[1], ids[0]]], ['CAPSULE', [ids[2], ids[0]]], ['ماشومانو', [ids[2]]], ['not-present', []], ['%', []], ['_', []]]) {
      const response = await medicationRequest(origin, owner, 'GET', { path: `/api/medications?search=${encodeURIComponent(search)}&pageSize=2` })
      assert.equal(response.status, 200)
      const found = await response.json()
      assert.equal(found.total, matches.length)
      assert.deepEqual(found.items.map(item => item.id), matches)
      assert.ok(found.items.every(item => item.user.id === owner.user.id))
    }
    const filtered = await (await medicationRequest(origin, owner, 'GET', { path: '/api/medications?search=capsule&page=2&pageSize=1' })).json()
    assert.deepEqual([filtered.total, filtered.page, filtered.pageSize], [2, 2, 1])
    assert.deepEqual(filtered.items.map(item => item.id), [ids[0]])
    const beyondLastPage = await medicationRequest(origin, owner, 'GET', { path: '/api/medications?page=2147483647&pageSize=100' })
    assert.equal(beyondLastPage.status, 200)
    assert.deepEqual((await beyondLastPage.json()).items, [])
    for (const query of ['page=0', 'page=-1', 'page=1.5', 'pageSize=0', 'pageSize=101', `search=${'x'.repeat(201)}`]) {
      assert.equal((await medicationRequest(origin, owner, 'GET', { path: `/api/medications?${query}` })).status, 400)
    }
    const literal = await (await medicationRequest(origin, owner, 'GET', { path: '/api/medications?search=%25' })).json()
    assert.equal(literal.total, 0, 'Search treats SQL wildcards as literal characters')
  }
})

test('medications reject invalid fields without creating or partially changing records', async () => {
  const owner = await account('medicine-validation')
  const fields = { name: 'Valid medicine', type: 'Tablet', quantity: 10, remarks: 'Original note' }
  const created = await medicationRequest(web, owner, 'POST', { body: fields })
  assert.equal(created.status, 201)
  const saved = await created.json()
  const invalidFields = [
    {}, null, [],
    { ...fields, name: '' }, { ...fields, name: '   ' }, { ...fields, name: 'x'.repeat(201) },
    { ...fields, type: '' }, { ...fields, type: '   ' }, { ...fields, type: 'x'.repeat(101) },
    { ...fields, quantity: -1 }, { ...fields, quantity: 1.5 }, { ...fields, quantity: null },
    { ...fields, quantity: '10' }, { ...fields, quantity: true },
    { name: fields.name, type: fields.type }, { ...fields, quantity: 2147483648 },
    { ...fields, remarks: 'x'.repeat(1001) }
  ]
  for (const origin of [api, web]) {
    for (const body of invalidFields) {
      const invalidCreate = await medicationRequest(origin, owner, 'POST', { body })
      assert.equal(invalidCreate.status, 400, `Invalid create at ${origin}: ${JSON.stringify(body)?.slice(0, 100)}`)
      const invalidUpdate = await medicationRequest(origin, owner, 'PUT', { path: `/api/medications/${saved.id}`, body })
      assert.equal(invalidUpdate.status, 400, `Invalid update at ${origin}: ${JSON.stringify(body)?.slice(0, 100)}`)
    }
    const list = await (await medicationRequest(origin, owner)).json()
    assert.equal(list.total, 1)
    assert.deepEqual(list.items, [saved], 'Rejected mutations leave the saved record unchanged')
    const maximum = await medicationRequest(origin, owner, 'PUT', {
      path: `/api/medications/${saved.id}`, body: { ...fields, quantity: 2147483647 }
    })
    assert.equal(maximum.status, 200)
    assertMedication(await maximum.json(), owner, { ...fields, quantity: 2147483647 })
    assert.equal((await medicationRequest(origin, owner, 'PUT', { path: `/api/medications/${saved.id}`, body: fields })).status, 200)
  }
})

test('medication endpoints require authentication and mutations reject cross-site requests', async () => {
  const owner = await account('medicine-csrf')
  const fields = { name: 'Protected medicine', type: 'Tablet', quantity: 1, remarks: '' }
  const created = await medicationRequest(web, owner, 'POST', { body: fields })
  assert.equal(created.status, 201)
  const saved = await created.json()
  const mutations = [['POST', '/api/medications'], ['PUT', `/api/medications/${saved.id}`]]
  for (const origin of [api, web]) {
    for (const path of ['/api/medications', `/api/medications/${saved.id}`]) {
      assert.equal((await fetch(origin + path)).status, 401)
    }
    for (const [method, path] of mutations) {
      const response = await fetch(origin + path, {
        method, headers: { 'content-type': 'application/json', origin, 'x-roghtia-request': '1' }, body: JSON.stringify(fields)
      })
      assert.equal(response.status, 401, `${method} ${origin}${path} without authentication`)
    }
  }
  for (const [method, path] of mutations) {
    for (const headers of [{ 'x-roghtia-request': '' }, { origin: 'https://untrusted.example' }]) {
      const response = await medicationRequest(web, owner, method, { path, body: { ...fields, name: 'Cross-site edit' }, headers })
      assert.equal(response.status, 403)
    }
  }
  const unchanged = await (await medicationRequest(api, owner)).json()
  assert.equal(unchanged.total, 1)
  assert.deepEqual(unchanged.items, [saved])
})
