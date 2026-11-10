# Implementation Roadmap — Phase 1

Status of the Phase-1 delivery. Every box is backed by code and tests; the evidence is in
`docs/RUNBOOK.md` (how to run) and `docs/DEVIATIONS.md` (what differs from the master prompt).

## Stage 0 — Repository and baseline

- [x] Read the SQL schema, migration, proposal, ERD sources, and Figma export.
- [x] Record open questions and assumptions (`docs/OPEN_QUESTIONS.md`).
- [x] Solution and four projects + two test projects (`HamzetWasl.slnx`).
- [x] Database created from `Hamzet_Wasl_Schema_v2.sql` (LocalDB locally, SQL Server container in CI).
- [x] EF Core model for all 60 tables (no migrations: the script is the source of truth).
- [x] ASP.NET Core Identity, JWT + rotating refresh tokens, Serilog, Swagger, FluentValidation, SignalR.
- [x] Response envelope, error middleware, validation filter, Arabic messages.
- [x] Schema-comparison tests: `SchemaMatchesSqlTests` (unit) and `SchemaAppliedTests` (integration).

## Stage 1 — Auth

- [x] `GET /api/v1/meta/account-types`
- [x] `POST /api/v1/auth/register` (all four public roles)
- [x] `POST /api/v1/auth/login`
- [x] `POST /api/v1/auth/google`
- [x] `POST /api/v1/auth/refresh`
- [x] `POST /api/v1/auth/logout`
- [x] `POST /api/v1/auth/forgot-password`
- [x] `POST /api/v1/auth/reset-password`
- [x] `POST /api/v1/auth/change-password`
- [x] Email OTP send/verify
- [x] Phone OTP send/verify
- [x] JWT claims, refresh-token rotation, account-status enforcement

## Stage 2 — Profiles and accessibility

- [x] `GET/PUT /api/v1/me/profile` (+ avatar upload)
- [x] `PUT /api/v1/me/interests`
- [x] `PUT /api/v1/me/skills`
- [x] `GET/PUT/DELETE /api/v1/me/accessibility`
- [x] `GET /api/v1/meta/governorates`, `/areas`, `/business-fields`, `/employee-ranges`, `/skills`, `/interests`, `/need-types`, `/accommodations`

## Stage 3 — Volunteer

- [x] Volunteer profile + location endpoints
- [x] Verification state machine and `GET /me/verification`
- [x] Evidence upload + encrypted storage keys
- [x] Social links
- [x] Interview slots and booking/change/cancel
- [x] Training modules/attempts
- [x] Availability slots

## Stage 4 — Organization

- [x] Organization onboarding, resumable
- [x] Logo/cover upload
- [x] Documents upload and replacement
- [x] Submit + review status
- [x] Approval/rejection notifications

## Stage 5 — Admin

- [x] Volunteer review queue + detail + decision
- [x] Organization review queue + detail + decision
- [x] Interview slot management
- [x] User suspension
- [x] Deletion requests
- [x] Audit log viewer
- [x] Signed short-lived evidence URLs

## Stage 6 — Verification

- [x] Unit tests for all application rules (98 tests)
- [x] Integration tests for Phase 1 flows (9 tests, real SQL Server)
- [x] Authorization/ownership tests
- [x] `dotnet build` clean (0 warnings, warnings-as-errors in CI); `dotnet test` green
- [x] `docs/API_CONTRACT.md`, `docs/openapi.json`, `docs/ERD_TO_API_MAP.md`, `docs/RUNBOOK.md`,
      `docs/DEVIATIONS.md`, `docs/CHANGELOG.md`
- [x] CI pipeline (`.github/workflows/ci.yml`): build, unit + integration tests on a SQL Server
      container, OpenAPI drift check, secret scan

Phase 2 begins only after Phase 1 review. The Phase-2 modules and their tables are listed in
`docs/ERD_TO_API_MAP.md`.
