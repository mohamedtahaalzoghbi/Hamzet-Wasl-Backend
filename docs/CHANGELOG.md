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

### The Phase 1 UI-alignment round (batch 2): review creation time

Schema change (approved): `VERIFICATION_REVIEW.created_at DATETIME2(0) NOT NULL DEFAULT
SYSUTCDATETIME()`.

- `Hamzet_Wasl_Schema_v2.sql` is updated; `Hamzet_Wasl_Migration_v2_1_review_created_at.sql`
  applies the same change to an existing database (backfill: `decided_at`, then `interview_at`,
  then the migration time; the script is idempotent and has a documented rollback).
- `VerificationReview.CreatedAt` is set from `IClock.UtcNow` when a review row is created.
- "The latest review" is now `ORDER BY created_at DESC, review_id DESC` in `VolunteerService`
  (the verification read and the get-or-create path). The admin queue uses `created_at` as the
  tie-breaker after `interview_at`; it was `review_id`.
- Regression: `ReviewOrderingTests` inserts two reviews per case whose ids are the inverse of
  their timestamps, so any return to `review_id` ordering fails the test (volunteer endpoint and
  admin queue).
- No API shape change: the endpoints and DTOs are untouched, so `docs/openapi.json` needs no
  regeneration.

### The Phase 1 UI-alignment round (batch 3): dynamic governorates and areas

Schema change (approved): the reference catalogue moves into the database.

- Two new tables in `Hamzet_Wasl_Schema_v2.sql`: `GOVERNORATE` (`governorate_id`, unique `code`,
  `name_ar`, `is_active`) and `AREA` (`area_id`, `governorate_id` FK, unique `(governorate_id,
  code)`, `name_ar`, `is_active`). Both default `is_active` to 1.
- `Hamzet_Wasl_Migration_v2_2_geo_tables.sql` creates them on an existing database and seeds the
  27 governorates and 41 areas from `MetaCatalog`; it is idempotent (creates only what is missing,
  seeds only an empty table) and carries a documented rollback.
- `DatabaseSeeder.SeedGeoAsync` fills the tables on the first run and only **adds** missing codes,
  so an administrator's edits are never overwritten.
- `IGeoCatalog` / `GeoCatalogService` (Infrastructure) read the active rows with a ten-minute
  `IMemoryCache`; a completely empty table falls back to the static `MetaCatalog` until seeding,
  and a table with rows but none active returns an empty list (an explicit admin choice).
- `GET /meta/governorates` and `GET /meta/governorates/{key}/areas` now read the database
  (same response shape; `{key}` accepts the code or the Arabic name; unknown → 404 `NOT_FOUND`).
- The governorate/area rules were removed from `Validators.cs` and moved to `VolunteerService`
  and `OrganizationService` (the same `governorate` / `serviceArea` / `city` field keys and
  messages). Codes are stored in `VOLUNTEER_PROFILE.governorate/service_area` and
  `ORGANIZATION.governorate/city`, so the API contract is unchanged.
- Regression: `GeoCatalogServiceTests` (unit: empty-table fallback + active-only filtering) and
  `GeoCatalogTests` (integration: meta lists, inactive-row hiding, and the moved profile
  validation, including Arabic-name normalisation). `SchemaMatchesSqlTests` and
  `SchemaAppliedTests` now expect 62 tables.
- No API shape change: `docs/openapi.json` needs no regeneration.

### The Phase 1 UI-alignment round (batch 4): one or more accessibility need types

No schema change: `ACCESSIBILITY_PROFILE` still has a single `need_type_id` column.

- `PUT /me/accessibility` accepts an optional `needTypes` list (1 to 3 values, no duplicates,
  first value must equal `needType`). Omitting it means `[needType]`, so existing clients are
  unaffected. The primary type remains in `need_type_id`; the extra types are stored in
  `manual_overrides.needTypes` as JSON (`["visual","hearing"]`). A single-type request removes a
  stale array, so switching from two needs back to one is clean.
- `manualOverrides` (the free-form preset override) is now limited to 4 KB (4096 bytes, UTF-8)
  and must still be valid JSON.
- `AccessibilityResponse` gains `needTypes` (the declared list, primary first); the calculated
  `effectivePreset.needTypes` continues to expose the same order. `manualOverrides.needTypes`
  written directly by older clients is still honoured when the new field is omitted.
- Regression: `AccessibilityOverridesTests` (unit: merge, stale-array removal, legacy JSON,
  ordering/deduplication, UTF-8 size limit), the new validator cases, and
  `AccessibilityNeedTypesTests` (integration: one type, two types, persistence, and 400 for four
  types with a `needTypes` field error).
- `docs/openapi.json` regenerated: the `needTypes` properties are arrays of the three allowed
  values in `UpsertAccessibilityRequest`, `AccessibilityResponse` and `AccessibilityPresetDto`
  (the schema filter previously published the preset list as a string).

### Public repository preparation

No API change: the contract, the schema and the behaviour are identical.

- `src/HamzetWasl.Api/appsettings.Development.json` is **no longer tracked**. The repository
  ships `appsettings.Development.example.json` and `appsettings.Production.example.json` with
  `DEV-ONLY-YOUR_..._HERE` placeholders, and the real files are ignored by `.gitignore`.
  `appsettings.json` keeps every credential empty (as before).
- The integration test host no longer depends on a local configuration file: the signing keys
  and the seeded admin are injected by the fixture (`HamzetWaslWebApplicationFactory`,
  `ApiFixture`), so a fresh clone runs the suite on any machine.
- The CI pipeline provides the signing keys through environment variables for the OpenAPI
  export, and its `no-secrets` job now checks the committed example files, scans the tracked
  sources and fails if a real configuration file is ever tracked again.
- The whole Git history was audited: no connection string with a password, no private key and
  no provider token; the only literals are the `DEV-ONLY` placeholders and the test fixtures.
- `README.md` was rewritten for the public audience (logo, setup steps, test commands,
  documentation index) and `docs/RUNBOOK.md` documents the copy-the-example step.

### Earlier changes inside v1

- `POST /me/verification/evidence` (was documented as `PUT`): the evidence type is part of the
  multipart body (`evidenceType` + `file`).
- Admin decisions **require** `notes` when rejecting (`VALIDATION_ERROR` otherwise).
- Availability slots are validated as future, ordered and non-overlapping.
- Interview slots must start in the future; duration stays 15–120 minutes.
- Volunteer `weeklyHours` is validated as 1–168.
- `serviceRadiusKm` is optional (the schema allows `NULL`); when present it must be 1–200.

