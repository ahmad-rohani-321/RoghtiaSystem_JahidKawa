# Roghtia · روغتیا

Nuxt 4, Vue 3 and Nuxt UI frontend with an ASP.NET Core 10 authentication API and encrypted SQLite storage.

## Run locally

From the repository root, start the API:

```powershell
dotnet run --project RoghtiaSystem_JahidKawa.Server --launch-profile http
```

In a second terminal:

```powershell
cd roghtiasystem_jahidkawa.client
npm install --legacy-peer-deps
npm run dev
```

Open http://localhost:64053/register to create an account, or http://localhost:64053/login to sign in. The API listens at http://localhost:5201. No seeded credentials are required. Development automatically creates the database and persists generated signing/database keys under the server's ignored `.local` directory. Retain that directory to preserve access to the development database and tokens.

## Authentication

- Login submits username, password and Remember Me to `POST /api/auth/login`.
- Registration submits username and password to `POST /api/auth/register` and starts a session.
- The Nuxt server forwards these requests to ASP.NET and places the returned JWT in an HttpOnly, SameSite=Strict cookie. Browser JavaScript never receives or stores the token.
- Remember Me creates a persistent cookie valid for up to 30 days. Without it, the cookie has no persistence attributes and lasts for the browser session; its token expires after 24 hours. Browsers with session restoration may restore session cookies according to their own settings.
- `GET /api/auth/me` verifies the token on the API and returns the current user. Protected pages verify the session before rendering. Logout clears the cookie.
- Usernames are trimmed, Unicode-normalized and compared without case sensitivity. A unique database index handles concurrent duplicate registrations. New passwords use PBKDF2 with a per-password salt.
- All new users receive the `User` role. API authorization defaults to requiring a valid token; future role restrictions can use `[Authorize(Roles = "...")]`. The frontend exposes role/permission helpers for presentation, while enforcement stays on the server.
- Auth requests include a same-origin mutation header; responses and authenticated HTML are not cached.

The browser uses same-origin Nuxt endpoints. The ASP.NET API still returns `{ token, expiresAt, user }` to direct API clients; the Nuxt response omits the JWT.

## Doctor information and password settings

Settings loads the signed-in user's doctor information and provides Pashto/English doctor names and professions, the hospital's Pashto name, and three image fields. Empty strings and no images are the defaults. Save updates the profile and top-bar doctor name immediately; the secondary line shows the actual username. Reset asks for confirmation, deletes only the user's saved profile and images, and restores the empty defaults.

`/api/doctor-information` supports GET, multipart POST (create), PUT (update), and DELETE. `POST /api/doctor-information/reset` returns the default profile. Ownership comes exclusively from the validated JWT user ID; request fields cannot select another user. PNG/JPEG/WebP uploads are checked against their signatures and limited to 2 MiB each (7 MiB total request). Images are stored as blobs in the existing encrypted database and served through authenticated, non-cached media routes; they are never placed in public storage.

The account dropdown opens the password-change form. `POST /api/auth/change-password` verifies the current password, hashes the new password, and increments the user's token version. All existing sessions, including remembered sessions, become invalid; the current cookie is cleared and the user signs in with the new password. This affects only the authenticated user.

## Medications

The Medications page lists only the signed-in user's records, with server-side search and pagination. It supports creating and updating medicines through the RTL Pashto form; mobile screens use cards. A medication has `Id`, `Name`, `Type`, `Quantity`, `Remarks` and a `User` association. The API generates the ID and assigns the owner from the verified session. Name and type are required (200/100 characters); quantity is a whole number from 0 to 2,147,483,647; remarks are optional (1,000 characters).

The same-origin Nuxt endpoints forward authenticated requests to ASP.NET:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/api/medications?page=1&pageSize=20&search=term` | Search the current user's name/type/remarks; page size is capped at 100. |
| GET | `/api/medications/{id}` | Read one owned medication. |
| POST | `/api/medications` | Create with `{ name, type, quantity, remarks }`. |
| PUT | `/api/medications/{id}` | Update those fields on an owned medication. |

Responses contain only the owner's ID and username. Foreign and missing IDs both return 404, and submitted user/ID fields cannot change ownership. There is no medication Delete endpoint, UI action or client function. Startup creates the medication table and owner index without replacing existing data.

## Configuration and deployment

Deploy the Nuxt `.output` Node application alongside the ASP.NET API. Authentication requires the Nuxt server; a static-only deployment is insufficient.

| Setting | Purpose |
| --- | --- |
| `NUXT_AUTH_API_BASE` | Private API origin; defaults to `http://localhost:5201`. |
| `NUXT_AUTH_COOKIE_SECURE` | `true` for HTTPS production; `false` only for local HTTP development. |
| `Auth__SigningKey` | Required production JWT secret, at least 64 bytes, provided by environment/secret manager. |
| `ConnectionStrings__MainDatabase` | Required production SQLite connection string, including encryption password. |
| `Auth__SessionHours` | Non-remembered token lifetime; default 24. |
| `Auth__RememberDays` | Remembered token lifetime; default 30. |
| `Auth__RateLimitPermitLimit` | Login/registration/password-change requests per minute per API peer address; default 20. |

Do not commit signing keys or database passwords. Use HTTPS for production. When the API sits behind Nuxt, its IP rate limit applies to the Nuxt peer; configure the deployment's trusted ingress rate limiting and size this limit for the clinic. Reverse proxies must preserve the public host/protocol for origin checks.

For an existing starter database, supply its original connection string/password. Startup adds normalized usernames, password/token versions, role columns and the doctor-information and medication tables transactionally. It stops on pre-existing normalized duplicate usernames without deleting accounts. Successful legacy ASCII-password logins upgrade their hashes; legacy non-ASCII hashes require a password reset because the starter encoded passwords as ASCII.

## Pages and design

Dashboard, Prescriptions, Patients, Medications, Reports and Settings use the shared RTL navigation. Login and Register remain standalone. Login adds Remember Me; registration has only username and password fields.

The dashboard uses synthetic data. Clinical records, clinical APIs and audit trails remain future work. Calendar calculations remain Jalali, with exact Pashto month names and compact Persian weekday labels. Both light/dark modes and responsive typography are retained.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the file structure.

## Validation

Patients supports authenticated `GET /api/patients`, `GET /api/patients/{id}`, `POST /api/patients` and `PUT /api/patients/{id}` through the Nuxt API proxy. The server assigns ownership from the verified token and generates immutable codes (`RT-1`, `RT-2`, ...) independently for each user. Creation serializes code allocation in a database transaction; a unique `(UserId, Code)` index enforces uniqueness. There is no patient Delete operation. Startup adds the patient table and indexes to existing databases.

Name, age (whole years, 0–150) and gender (`Male`, `Female`, `Other`) are required. Phone (up to 32 characters) and address (up to 1000) are optional. The RTL page includes a responsive list, name/code/phone search, pagination and a shared create/edit form. User ownership remains internal, and code is read-only. Integration checks cover ownership, validation, concurrent code allocation and sequence persistence after restart.

```powershell
# From the repository root:
dotnet build RoghtiaSystem_JahidKawa.Server -p:BuildProjectReferences=false
cd roghtiasystem_jahidkawa.client
npm run build
npm run test:auth
```

The integration suite launches both real servers with isolated temporary databases. It covers login/registration, duplicate and concurrent usernames, Unicode passwords, cookie lifetimes, expiry/forged tokens, server restarts, legacy schema upgrades, rate limiting, protected HTML, account-page structure, doctor CRUD ownership, private uploads, reset, password changes and session revocation. Medication coverage includes owner-scoped create/read/update, validation, search/pagination, persistence and rejected Delete requests. It never changes the application's database. Browser visual testing is separate from these HTTP integration checks.

