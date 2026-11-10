# Implementation Roadmap — Phase 1

## Stage 0 — Repository and baseline

- [x] Read the SQL schema, migration, proposal, ERD sources, and Figma export.
- [x] Record open questions and assumptions.
- [ ] Create solution and four projects + two test projects.
- [ ] Create the database from `Hamzet_Wasl_Schema_v2.sql` (LocalDB).
- [ ] Scaffold/implement the EF Core model for all 60 tables.
- [ ] Configure ASP.NET Core Identity, JWT, Serilog, Swagger, FluentValidation, SignalR.
- [ ] Add the response envelope, error middleware, validation filter, Arabic messages.
- [ ] Add schema-comparison integration test.

## Stage 1 — Auth

- [ ] `GET /api/v1/meta/account-types`
- [ ] `POST /api/v1/auth/register` (all four public roles)
- [ ] `POST /api/v1/auth/login`
- [ ] `POST /api/v1/auth/google`
- [ ] `POST /api/v1/auth/refresh`
- [ ] `POST /api/v1/auth/logout`
- [ ] `POST /api/v1/auth/forgot-password`
- [ ] `POST /api/v1/auth/reset-password`
- [ ] `POST /api/v1/auth/change-password`
- [ ] Email OTP send/verify
- [ ] Phone OTP send/verify
- [ ] JWT claims, refresh-token rotation, account-status enforcement

## Stage 2 — Profiles and accessibility

- [ ] `GET/PUT /api/v1/me/profile`
- [ ] `PUT /api/v1/me/interests`
- [ ] `PUT /api/v1/me/skills`
- [ ] `PUT/GET/DELETE /api/v1/me/accessibility`
- [ ] `GET /api/v1/meta/governorates`, `/areas`, `/business-fields`, `/employee-ranges`, `/skills`, `/interests`, `/need-types`, `/accommodations`

## Stage 3 — Volunteer

- [ ] Volunteer profile + location endpoints
- [ ] Verification state machine and `GET /me/verification`
- [ ] Evidence upload + storage keys
- [ ] Social links
- [ ] Interview slots and booking/change/cancel
- [ ] Training modules/attempts
- [ ] Availability slots

## Stage 4 — Organization

- [ ] Organization onboarding, resumable
- [ ] Logo/cover upload
- [ ] Documents upload and replacement
- [ ] Submit + review status
- [ ] Approval/rejection notifications

## Stage 5 — Admin

- [ ] Volunteer review queue + detail + decision
- [ ] Organization review queue + detail + decision
- [ ] Interview slot management
- [ ] User suspension
- [ ] Deletion requests
- [ ] Audit log viewer
- [ ] Signed short-lived evidence URLs

## Stage 6 — Verification

- [ ] Unit tests for all application rules
- [ ] Integration tests for Phase 1 flows
- [ ] Authorization/ownership tests
- [ ] `dotnet build` clean; `dotnet test` green
- [ ] `docs/API_CONTRACT.md`, `openapi.json`, `ERD_TO_API_MAP.md`, `RUNBOOK.md`

Phase 2 begins only after Phase 1 review.
