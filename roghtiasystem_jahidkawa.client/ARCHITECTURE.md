# Roghtia architecture

## Frontend

- `app/pages`: dashboard, doctor Settings and owned Medications; standalone login/register; empty prescriptions, patients and reports routes.
- `app/composables/useMedications.ts`, `shared/types/medication.ts` and `server/api/medications`: typed, authenticated medication create/read/update, with bounded input validation in `server/utils/medications.ts`. List state is local to the page and has no cross-user cache. No medication Delete operation exists.
- `app/components/AccountForm.vue`: shared Nuxt UI form, password visibility, field validation, Remember Me for login, pending state and localized error messages.
- `app/components/SettingsImageInput.vue` and `PasswordChangeModal.vue`: reusable native Nuxt UI upload/preview controls and authenticated password form.
- `app/composables/useDoctorInformation.ts`: user-bound shared profile state, loading, save and reset; top-bar identity reacts to saved changes.
- `server/api/doctor-information`, `server/utils/owned-api.ts` and `shared/types/doctor-information.ts`: authenticated multipart CRUD/media boundary, bounded request bodies and shared response contracts.
- `app/composables/useAuth.ts`: server-verified identity, login/registration/logout, roles and permission helpers. It never exposes a JWT.
- `app/middleware/auth.global.ts`: session validation before protected routes, including SSR.
- `app/plugins/auth.client.ts`: expiry and focus checks.
- `server/api/auth` and `server/utils/auth.ts`: same-origin API boundary, mutation origin checks, private ASP.NET forwarding, HttpOnly cookie issuance and cleanup.
- `shared/types/auth.ts`: shared request/session/user types.
- `app/layouts/default.vue`: current user's identity, logout and responsive floating navigation.
- `app/composables/useJalali.ts`: PersianCalendar arithmetic and shared localized labels.
- `app/assets/css/main.css`: design tokens, normal-size typography, responsive layout and light/dark styling.

Persistent auth cookies last at most the JWT lifetime. Session cookies omit Max-Age/Expires. Nuxt forwards only the JWT as a Bearer header to ASP.NET, validates identity through `/api/auth/me`, and sends only user data and expiry to the browser. UI state does not authorize API access.

## ASP.NET API

- `Controllers/AuthController.cs`: POST login/register, protected GET me and POST change-password.
- `Controllers/DoctorInformationController.cs`, `Models/DoctorInformation.cs` and `Services/DoctorImages.cs`: one owned profile per user, validated image blobs and protected media delivery.
- `Controllers/MedicationsController.cs`, `Data/Models/Medication.cs` and `ViewModels/MedicationRequest.cs`: user-scoped medication queries, paginated search, safe owner projections, and validated create/update requests. The user relationship is assigned by the API and cannot be reassigned through request data.
- `Services/PasswordService.cs`: PBKDF2 password hashing and legacy ASCII HMAC upgrades.
- `Services/TokenService.cs` and `AuthOptions.cs`: JWT issuance, lifetimes and environment-based secrets; persisted protected development secrets.
- `Services/UserNames.cs`: consistent normalization.
- `Data/MainDbContext.cs` and `Models/Users.cs`: unique normalized username index, password metadata and server-assigned role.
- `Data/DatabaseInitializer.cs`: transactional startup compatibility with the original schema.
- `Program.cs`: JWT validation, default authorization, auth rate limiting and SQLCipher database setup.

New users receive only the User role. Future authorization policies belong on API endpoints. Permission arrays are reserved for that extension; clients cannot grant roles by posting extra registration fields. JWT validation checks the stored user's token version on every authenticated request. Password changes increment this version with a concurrency check, invalidating all prior sessions. Logout clears the current browser cookie; independent per-token revocation remains a future extension.

## Calendar and dashboard

Exact month labels: وری، غویی، غبرګولی، چنګاښ، زمری، وږی، تله، لړم، لیندۍ، مرغومی، سلواغه، کب.

Weekday header labels: شنبه، یک، دو، سه، چهار، پنج، جمعه. The compact picker retains Jalali calculations, Persian numerals and Saturday-first weeks.

Dashboard widgets persist only their order/visibility in localStorage. No credentials or patient records are placed there. Clinical widgets still use synthetic data.

## Tests and operations

`tests/auth.integration.test.mjs` starts real Nuxt/ASP.NET processes against temporary databases, using random test secrets. Tests verify API contracts, uniqueness races, password validation, JWT rejection, cookie persistence/security, SSR authorization, server restarts and legacy upgrade behavior. Medication tests cover create/read/update ownership, safe responses, pagination/search, invalid input, CSRF and the absence of a Delete endpoint.

See [README.md](README.md) for startup, production settings and existing-database migration notes.

