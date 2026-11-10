# Changelog

Contract history for the Hamzet Wasl API. The Flutter and React clients pin to a published
version; breaking changes only happen under a new `/api/v2` (MASTER_PROMPT §3).

## v1 — Phase 1 (current)

Published contract: `docs/openapi.json` (OpenAPI 3.0.4) and `docs/API_CONTRACT.md`.

| Area | Endpoints |
|---|---|
| Auth | register, login, google, refresh, logout, forgot/reset/change password, email + phone OTP, session |
| Meta | account types, governorates (+areas), business fields, employee ranges, skills, interests, need types, accommodations |
| Profile | own profile, avatar, interests, skills |
| Accessibility | `GET/PUT/DELETE /me/accessibility` (disabled_user only) |
| Volunteer | profile, location, verification, evidence upload, social links, interview booking (book/change/cancel), availability, training |
| Organization | profile, logo, cover, documents, submit, review status |
| Admin | volunteer and organization review queues + decisions, interview slot management, user suspension, deletion requests, audit log |
| Files | signed private file access |
| Realtime | `/hubs/notifications` |

### Changes inside v1 (before the first client integration)

- `POST /me/verification/evidence` (was documented as `PUT`): the evidence type is part of the
  multipart body (`evidenceType` + `file`).
- Admin decisions now **require** `notes` when rejecting (`VALIDATION_ERROR` otherwise).
- Availability slots are validated as future, ordered and non-overlapping.
- Interview slots must start in the future; duration stays 15–120 minutes.
- Volunteer `weeklyHours` is validated as 1–168.
- `serviceRadiusKm` is optional (the schema allows `NULL`); when present it must be 1–200.
