# Changelog

Contract history for the Hamzet Wasl API. The Flutter and React clients pin to a published
version; breaking changes only happen under a new `/api/v2` (MASTER_PROMPT §3).

## v1 — Phase 1 (current)

Published contract: `docs/openapi.json` (OpenAPI 3.0.4) and `docs/API_CONTRACT.md`.

| Area | Endpoints |
|---|---|
| Auth | register, login, google, refresh, logout, forgot/reset/change password, email + phone OTP, session |
| Meta | account types, governorates (+areas), business fields, employee ranges, skills, interests, need types, accommodations |
| Profile | own profile, avatar, interests, skills, deletion request, notification centre (history + read) |
| Accessibility | `GET/PUT/DELETE /me/accessibility` (disabled_user only) |
| Volunteer | profile, location, verification, evidence upload, social links, interview booking (book/change/cancel), availability, training |
| Organization | profile, logo, cover, documents, submit, review status |
| Admin | volunteer and organization review queues + decisions, interview slot management, user suspension, deletion requests, audit log |
| Files | signed private file access |
| Realtime | `/hubs/notifications` |

### The P0 hardening round (before the first client integration)

Security and contract fixes, all covered by regression tests:

- **Access tokens only**: the tokens carry a `purpose` claim and the bearer pipeline rejects a
  refresh token or a Google registration ticket (401). The refresh rotation and the logout
  revocation are unchanged.
- **UTC everywhere**: every timestamp is serialised with the `Z` suffix, including the values
  that are read back from SQL Server (`DATETIME2` is materialised without a `Kind`).
- **OpenAPI accuracy**: the document now carries the real status codes (201 for the creations,
  204 for the empty answers), the common error responses with the standard envelope, the correct
  bearer requirement per operation (51 protected, 16 public) and the allowed values of the
  enumerated fields.
- **SignalR contract**: the client method is `notificationReceived` and the payload is
  `{id, type, titleAr, bodyAr, createdAt, payloadJson}`. The interview events and the suspension
  are published now, so every documented event really happens.
- **Accessibility preset**: `effectivePreset` is calculated (need-type defaults → the user's own
  preferences → manual overrides) and supports several needs through `manualOverrides.needTypes`.
- **Upload limits**: an oversized file answers 413 `FILE_TOO_LARGE`, an oversized request answers
  413 `REQUEST_TOO_LARGE`, and no framework generated English message reaches the client.
- **Rate limiting**: the limiter runs after the authentication (so the limits are per identity),
  the forwarded headers are honoured, and the 429 answer carries `Retry-After`.
- **Account lockout**: five failed logins lock the account for 15 minutes.
- **Phone ownership**: the number is only written to the account after the code proves it.
- **Enumeration**: the reset-password endpoint answers the same way for an unknown address, and
  the phone endpoint no longer reveals which numbers are registered.
- **Logs and exposure**: Serilog never writes the request URLs (they carry the file signatures and
  the SignalR token), the Swagger document is only served in development, and no email or phone
  reaches the log file.

### The Phase 1 UI-alignment round (batch 1)

Endpoints added to close the two functional gaps found in the review. **No schema change**: every
column used here already exists in `Hamzet_Wasl_Schema_v2.sql`.

- `POST /me/deletion-request` creates the caller's `pending` `DELETION_REQUEST` row (201 with
  `{deletionRequestId, status, requestedAt}`; 409 `DELETION_REQUEST_ALREADY_PENDING` while one is
  pending). The admin queue and the anonymisation already existed; only the write side was missing.
- `GET /me/notifications` returns the caller's notifications newest first (paged, `pageSize`
  1..100) and `PUT /me/notifications/{id}/read` writes `read_at` (idempotent; 404 for an unknown
  id or another user's id). `payloadJson` stays a live SignalR field until a `payload_json`
  column is approved.
- Both flows are pinned by `Phase1GapTests` (4 integration tests): 201 then 409 plus the admin
  queue, ordering and paging, IDOR isolation, and read idempotency.
- `docs/openapi.json` regenerated: 59 paths / 69 operations / 116 schemas.

### Earlier changes inside v1

- `POST /me/verification/evidence` (was documented as `PUT`): the evidence type is part of the
  multipart body (`evidenceType` + `file`).
- Admin decisions **require** `notes` when rejecting (`VALIDATION_ERROR` otherwise).
- Availability slots are validated as future, ordered and non-overlapping.
- Interview slots must start in the future; duration stays 15–120 minutes.
- Volunteer `weeklyHours` is validated as 1–168.
- `serviceRadiusKm` is optional (the schema allows `NULL`); when present it must be 1–200.

