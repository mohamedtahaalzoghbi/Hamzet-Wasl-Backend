# PROMPT — Build the Hamzet Wasl (همزة وصل) Backend in ASP.NET Core

You are a senior .NET backend engineer. Build the complete backend for **Hamzet Wasl**, an inclusive platform for people with disabilities (Egypt, Arabic-first). A Flutter mobile app and a React web app will consume your API, so **the API contract is the product**: it must be exact, stable and fully documented.

## 0. Attached files — read ALL of them before writing any code

| File | Role |
|---|---|
| `Hamzet_Wasl_Schema_v2.sql` | **Source of truth for the database.** 60 tables, constraints, indexes. Do not change table/column names, types, nullability, CHECK values, or relationships. |
| `Hamzet_Wasl_ERD_Chen_All.pdf` / `Hamzet_Wasl_ERD_Light.pdf` | Same model as diagrams (5 pages: overview + 4 modules). |
| `Hamzet_Wasl_Project_Proposal_AR_Plain.docx` | Product scope, features, roles, user stories, tools. |
| `graduation.pdf` (Figma export, 54 pages) | The only UI designed so far: onboarding, account type, sign-up, OTP, verification, profile completion, company/volunteer registration, interview booking. |

If these files disagree, the **SQL schema wins for data**, the **Figma export wins for the screens it contains**, and the proposal wins for everything else. **Never silently guess.** If something is missing or contradictory, list it under "Open questions" in `docs/OPEN_QUESTIONS.md`, pick the most conservative option, and mark it `// ASSUMPTION:` in code.

## 1. Non-negotiable constraints

1. **Database = the SQL script.** EF Core entities and configurations must map to the exact table and column names (snake_case) in the script. Run the script (or an initial migration that produces an identical schema) and add an automated test that fails if the EF model and the SQL schema differ (tables, columns, nullability, FKs, unique constraints, CHECK values).
2. **Stack:** ASP.NET Core on the current .NET LTS/stable SDK (target .NET 10 if available, otherwise the latest), EF Core with SQL Server, ASP.NET Core Identity, JWT bearer + refresh tokens, FluentValidation, Serilog, Swagger/OpenAPI, SignalR (real-time chat/notifications), xUnit tests.
3. **Identity:** `USER_ACCOUNT` is the Identity user table (extend `IdentityUser<Guid>` or link 1:1). Password hashes live in Identity, never in a custom column. `user_id` is a `Guid` everywhere.
4. **No cascade deletes** (all FKs are NO ACTION). Deletion = `DELETION_REQUEST` flow with anonymisation in application code.
5. **Roles come only from `ACCOUNT_ROLE`:** `disabled_user`, `volunteer`, `association`, `company`, `admin`. Permissions derive from the role only. `admin` is never created through public registration (seed/CLI only).
6. **Exactly one disability type per user** (`visual`, `motor`, `hearing`) stored in `ACCESSIBILITY_PROFILE.need_type_id` with a `consent_id`. Disability type changes **only the UI preset**, never permissions. Selecting it is **skippable** in the app (profile row is created only when chosen). Only `disabled_user` accounts may have an accessibility profile. If the linked consent is withdrawn, the profile must be removed/reset.
7. **Privacy/safety:** never store medical documents; verification evidence is stored only as encrypted storage keys with `retention_until`; the API must never return other users' phone, email, ID images, or exact location (only approximate area). Log admin actions in `ADMIN_AUDIT`.
8. **Language:** all user-facing error/validation messages in **Arabic** (with a stable English `code`). Accept `Accept-Language`; default `ar`.
9. **Business rules that the SQL cannot enforce — implement and test them in the application layer:**
   - `ORGANIZATION.organization_type` must match the owner's role (`association` ↔ `association`, `company` ↔ `company`).
   - Only `association` organizations own `DIRECTORY_ITEM`; only `company` organizations own `JOB`.
   - Only an **approved** organization (`approval_status = approved`) can publish jobs/directory items. New org accounts start `account_status = pending_approval`.
   - `HELP_ASSIGNMENT` requires both sides to accept (`requester_accepted_at`, `volunteer_accepted_at`); `HELP_CONVERSATION` opens only after assignment. One assignment per request; one conversation per request.
   - Only a **verified** volunteer (verification flow complete + approved) can submit `HELP_OFFER`. `help_mode` of the volunteer (`online_only` / `online_and_field`) must be compatible with the request (`online` / `field`).
   - A user cannot rate or report themselves; `USER_FOLLOW` has no self-follow.
   - Interview slots: one `INTERVIEW_SLOT` ↔ at most one `VERIFICATION_REVIEW` (unique); booking sets slot `status = booked`; changing the appointment frees the old slot. Race-safe (transaction + unique index).
   - OTP: hashed code, expiry, max attempts, resend cool-down, rate limiting per user/IP/phone (`EMAIL_VERIFICATION`, `PHONE_VERIFICATION`).
   - Phone numbers: normalise to E.164 (Egyptian mobiles `01xxxxxxxxx` → `+201xxxxxxxxx`).
   - Uploads: allowed types/sizes exactly as the Figma export says (images PNG/JPG up to 5 MB for logo/cover/avatar; PDF or image up to 10 MB for company documents). Validate magic bytes, not just extension.
   - Rating is 1–5; JSON in `manual_overrides` must be valid JSON.

## 2. Architecture

Clean Architecture, one solution:
```
src/
  HamzetWasl.Domain          (entities, enums, domain rules)
  HamzetWasl.Application     (use cases, DTOs, validators, interfaces)
  HamzetWasl.Infrastructure  (EF Core, Identity, email/SMS/storage/push providers)
  HamzetWasl.Api             (controllers, middleware, SignalR hubs, Swagger)
tests/
  HamzetWasl.UnitTests
  HamzetWasl.IntegrationTests   (Testcontainers or LocalDB; real SQL Server)
docs/
  API_CONTRACT.md  OPEN_QUESTIONS.md  ERD_TO_API_MAP.md  RUNBOOK.md
```
Abstract every external dependency behind an interface with a development fake: `IEmailSender`, `ISmsSender`, `IFileStorage` (local disk in dev, S3/Azure Blob-compatible in prod), `IPushSender` (FCM), `IGoogleTokenValidator`, `IClock`. Configuration via `appsettings` + environment variables; no secrets in the repo.

## 3. API conventions (this is what Flutter and the web will rely on)

- Base path `/api/v1`. RESTful, plural nouns, JSON `camelCase`, ISO-8601 UTC timestamps, GUID ids.
- **One response envelope for every endpoint**:
  ```json
  { "success": true, "data": { }, "error": null, "traceId": "..." }
  { "success": false, "data": null, "error": { "code": "OTP_EXPIRED", "message": "انتهت صلاحية الرمز", "fields": { "phone": ["..."] } }, "traceId": "..." }
  ```
  Use correct HTTP status codes (400 validation, 401, 403, 404, 409 conflict, 422 business rule, 429 rate limit). Stable machine-readable `code` values listed in `docs/API_CONTRACT.md`.
- Pagination: `?page=1&pageSize=20` → `{ items, page, pageSize, totalCount }`. Filtering/sorting by explicit query params only.
- Auth: `Authorization: Bearer <access JWT>` (short-lived) + rotating refresh token (stored hashed). Claims: `sub`, `role`, `accountStatus`, `emailVerified`, `phoneVerified`.
- **Enums are serialized as lowercase snake_case strings exactly as the SQL CHECK values** (`online_only`, `full_time`, `pending_approval`, …).
- Every DTO field name must be traceable to a column or a documented computed field; maintain `docs/ERD_TO_API_MAP.md` (table → endpoints → DTO).
- Swagger/OpenAPI 3 at `/swagger`, with examples for every request/response, auth scheme, error responses, and `operationId`s that are stable (so Dart/TypeScript clients can be generated). Export `openapi.json` into `docs/` and keep it in sync (CI check).
- Versioning: breaking changes only under a new `/api/v2`.
- Health checks `/health`, CORS configurable, HTTPS only, security headers, request size limits.

## 4. Scope — Phase 1 (screens that already exist in Figma — build and test completely first)

### 4.1 Onboarding & registration
- `GET /meta/account-types` (company, disabled_user, association, volunteer — names/descriptions as shown in the app), `GET /meta/governorates` and areas (e.g. "القاهرة، مدينة نصر"), `GET /meta/business-fields` (technology, education, health, marketing, finance, engineering, other), `GET /meta/employee-ranges` (1–10, 11–50, 51–200, 200+), skills list, interests list, need types, accommodation types.
- `POST /auth/register` per account type (role chosen at registration, not changeable later):
  - disabled_user / volunteer: name, email, phone, password, confirm password, terms accepted (creates a `CONSENT_RECORD` with `policy_version`).
  - association / company: organization name (mapped to org + display name), official email, phone, password, terms accepted.
  - Password rule: ≥ 8 chars (shown in UI). Duplicate email/phone → 409 with field-level message.
- `POST /auth/login`, `POST /auth/google` (Google sign-in; account type still required for first-time users), `POST /auth/refresh`, `POST /auth/logout`, `POST /auth/forgot-password`, `POST /auth/reset-password`, `POST /auth/change-password`.
- Email verification: `POST /auth/email/send-code`, `POST /auth/email/verify`. Phone verification: `POST /auth/phone/send-code`, `POST /auth/phone/verify` (6-digit SMS OTP; wrong code returns a distinct error code and remaining attempts; resend supported).

### 4.2 Disabled user
- `GET/PUT /me/profile` (`USER_PROFILE`: display_name, bio, city, avatar upload, visibility), `PUT /me/interests`, `PUT /me/skills` (`USER_INTEREST`, `USER_SKILL`).
- `PUT /me/accessibility` (need type one of visual/motor/hearing + consent + theme, font_scale, reduced_motion, interaction_mode, notification_mode, manual_overrides), `GET /me/accessibility` (returns the effective preset so the client can render the right interface), `DELETE /me/accessibility`.

### 4.3 Volunteer
- Profile: avatar, introduction ("نبذة شخصية"), interests ("مجالات المساعدة"), skills, experience/certificates text (may contain a link), skill level (beginner/intermediate/advanced), weekly hours, help mode (`online_only` / `online_and_field`).
- Location: governorate, area (`service_area`), map point (latitude/longitude), `service_radius_km` (1–200; presets 5/10/20/30/50 + custom). `GET /me/volunteer-location` / `PUT`.
- Identity verification flow (state machine on `VERIFICATION_REVIEW`): explain → email verified → phone verified → upload national ID front/back (`VERIFICATION_EVIDENCE.evidence_type = national_id_front|national_id_back`) → social links (`VOLUNTEER_SOCIAL_LINK`: facebook/linkedin/instagram, used for review only, never public) → face photo (`face_photo`; the client captures it and uploads an image — the backend only receives and stores it encrypted) → book interview → admin decision. Expose `GET /me/verification` with current step, completed steps and status so the app resumes at the right screen.
- Interview booking: `GET /interview-slots?date=` (available days/times, unavailable ones flagged), `POST /me/verification/interview` (book), `PUT` (change later), `DELETE` (cancel). Admin manages `INTERVIEW_SLOT`s.
- Training modules/attempts (`TRAINING_MODULE`, `TRAINING_ATTEMPT`) and availability slots (`AVAILABILITY_SLOT`).

### 4.4 Association / Company
- Organization onboarding in steps, resumable: basic info (field, description, employee range, governorate + area) → profile assets (logo required, cover optional) → verification documents (`ORGANIZATION_DOCUMENT`: commercial_register, tax_card, address_proof — all required before submitting) → submit (creates `ORGANIZATION_REVIEW`, `approval_status = pending`; the UI promises a review within 24–48 hours) → admin approve/reject with notes → notification to the owner.
- Endpoints: `GET/PUT /me/organization`, `PUT /me/organization/logo|cover`, `PUT /me/organization/documents/{type}`, `POST /me/organization/submit`, `GET /me/organization/review-status`.

### 4.5 Admin
- Review queues for volunteers and organizations (list, detail with signed short-lived evidence URLs, approve/reject with notes), interview slot management, user suspension, deletion requests, audit log viewer. Every action writes `ADMIN_AUDIT`.

## 5. Scope — Phase 2 (modules in the ERD whose screens are NOT designed yet)

Implement the data model, services and endpoints from the ERD/SQL for these, with full validation, tests and OpenAPI docs, but mark each one in `API_CONTRACT.md` as **`status: provisional — UI not designed`** (field sets may change once Figma exists):
- **Help (module 2):** `HELP_REQUEST` (category, urgency, scheduled_at, optional voice note, approximate area, optional skill), offers, assignment, conversation + messages (REST + SignalR; text or audio), feedback (`rated_user_id`), safety reports (`reported_user_id`).
- **Jobs & directory (module 3):** jobs (employment_type, work_arrangement, location, accessibility_information, accommodations, expiry), applications (cv, voluntarily shared needs, status history in `APPLICATION_STATUS_CHANGE`), directory items (service/program/event), service inquiries + messages.
- **Social, places, safety (module 4):** communities, posts (media with alt text/transcript/captions), communities, memberships, follows, content reports + moderation, notifications (in_app/push/visual_alert/vibration) with FCM, emergency contacts, SOS alerts + deliveries (retention), places + facilities + reviews, obstacle reports, assistant log (assistant/speech_to_text/text_to_speech), feature registry.

Accessibility requirement for API design: every media-bearing response includes alternative text/transcript/captions fields; notifications carry the channel so the client can choose visual/vibration alerts for deaf users.

## 6. Quality bar (definition of done)

- `dotnet build` with zero warnings treated as errors for the Api project; `dotnet test` green.
- Unit tests for every business rule in §1.9; integration tests (real SQL Server) for each Phase-1 flow end-to-end (register → verify → profile → verification → admin approval).
- Authorization tests proving each role can only access its endpoints, and that users can only read/write their own data.
- Security: parameterised queries only (EF), input validation on every DTO, rate limiting on auth/OTP/upload, no PII in logs, secrets from env.
- Docs: `API_CONTRACT.md` (every endpoint: method, path, role, request, response, error codes, status provisional/stable), `openapi.json`, `ERD_TO_API_MAP.md`, `RUNBOOK.md` (setup, migrations, seed, env vars, how to run tests).
- Seed data: roles, need types (visual/motor/hearing), facility types, accommodations, skills, interests, governorates/areas, sample training module, one admin from env vars.

## 7. How to work

1. First reply: summarise the model in your own words (modules, roles, state machines), list `OPEN_QUESTIONS` and your assumptions. Do not start coding until you have done this.
2. Build Phase 1 end-to-end in vertical slices (auth → profile → volunteer verification → organization onboarding → admin), committing per slice, then Phase 2 per module.
3. After each slice update `API_CONTRACT.md` and `openapi.json`. The frontend/Flutter teams will start from these files, so never change a published contract field without bumping the version and noting it in a `CHANGELOG`.
4. Do not add features, tables, columns or endpoints that are not in the attached files. If you think something is missing, add it to `OPEN_QUESTIONS.md` instead of building it.
