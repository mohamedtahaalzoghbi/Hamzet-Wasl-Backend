# Changelog

## Post images, full profiles, direct chat (v2.6)
- Posts can carry images: `POST /api/v1/social/posts/{id}/media` (multipart, author only, up to 4 per post). Alt text is required for every image (the `POST_MEDIA` check constraint already enforces it; screen readers read it). Feed and post detail now return `media: [{mediaId, url, altText}]`.
- `GET /api/v1/social/users/{id}` now returns the full public profile: bio, city, avatar, memberSince, and a role block — `volunteer` (introduction, governorate, service area, weekly hours, skill level, verification status, skills, interests), `organization` (name, type, field, description, location, website, logo, approval status) for association/company/shop, or `needType` for a person with a disability. Private profiles show identity fields only to other users.
- New direct chat between accounts under `/api/v1/chat` (all member roles, admin excluded): list conversations, open a conversation with a user, read and send messages, mark read. One conversation per pair; the recipient gets a `chat_message_received` notification (notification centre + SignalR).
- New tables `CHAT_CONVERSATION` and `CHAT_MESSAGE`. Existing databases are upgraded at startup by `SchemaUpgrades` (idempotent). Migration file: `db/migrations/Hamzet_Wasl_Migration_v2_6_chat.sql`.
- New `Member` authorization policy: every non-admin role including `shop`.

## Shops (v2.5)
- New `shop` account role and `shop` organization type. Shops register and are approved by admin like companies.
- New table `SHOP_PRODUCT` (name, description, optional price, available). Listing and contact only, no orders or payments.
- New endpoints under `/api/v1/shops` (public list and detail, shop owner product CRUD).
- Jobs endpoints now use a `JobPoster` policy (association, company), so shops cannot post jobs.
- Existing databases are upgraded at startup by `SchemaUpgrades` (idempotent). Migration file: `db/migrations/Hamzet_Wasl_Migration_v2_5_shops.sql`.

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

### Staged registration (owner-approved breaking change, November 24, 2026)

The Flutter integration reported that `/auth/register` created the account — and reserved
the phone and email — before any OTP check, so an abandoned attempt blocked the next one
with 409. The owner confirmed the required order: data → send phone OTP → verify → **only
then create the account**.

- `POST /auth/register` now returns `200 RegisterPending` (an opaque `registrationTicket`,
  expiry and the normalized phone) instead of `201 Auth`. Nothing is written to the
  database at this step; the pending state is a short-lived server-side cache record and
  the ticket carries no personal data and no password (only a pre-computed password hash is
  kept server-side).
- New `POST /auth/register/complete` (`201 Auth`) and `POST /auth/register/resend` (`204`).
  The ticket is single-use; unknown/consumed/expired tickets get 422 `REGISTRATION_EXPIRED`.
- First-time Google sign-ups no longer create the account directly: they return
  `phoneVerificationRequired` (`RegisterPending`) and finish through
  `/auth/register/complete`, so phone ownership is proven there too. The completed Google
  account starts with `emailVerified: true`.
- This intentionally breaks the v1 register contract before the first client release, with
  the client's developer in the loop; the Flutter client must move to the two-step flow.

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

### The design-first review-fix round (November 22, 2026): race safety and retention

Schema change (owner-requested review-fix round): the filtered unique index
`UX_VERIFICATION_REVIEW_volunteer_open` on `VERIFICATION_REVIEW(volunteer_id) WHERE status IN
('draft','pending','interview_scheduled')` (script + EF model; migration
`Hamzet_Wasl_Migration_v2_3_open_review_index.sql` for existing databases). One open review
round per volunteer; closed rounds keep their history, so re-application stays allowed.

- **Exactly-once decisions**: both volunteer and organization decisions are conditional
  atomic updates, so two concurrent decisions can no longer both succeed. Eligibility is
  unchanged (`pending` or `interview_scheduled` for volunteers, `pending` for
  organizations); requiring the interview to have happened first is an open design
  question (Q24), not implemented.
- **Race-safe interview flow**: booking, changing and cancelling use conditional atomic
  updates on the slot and the review inside one transaction, so two concurrent bookings
  can no longer orphan a slot, and a concurrent cancel or decision cannot split the slot
  release from the review update. First-upload review creation treats the unique-index
  violation as a re-read. The cancel state transition itself is unchanged.
- **Retention implemented** (Q13): replaced evidence files are deleted from storage at
  replacement time, and `EvidenceRetentionCleanupService` (hosted,
  `Retention:CleanupIntervalHours`, default 24 h) deletes the encrypted file and row once
  `retention_until` passes on a closed review. Evidence of an open round is never deleted.
- **Reviewer notifications**: the slot owner is notified on booking, change and cancel
  (`interview_booked` / `interview_changed` / `interview_cancelled`).
- **Account deletion no longer rewrites `created_at`**: the anonymisation leaves
  `USER_ACCOUNT.created_at` untouched instead of stamping the deletion time.
- **Documentation honesty**: `DEVIATIONS` #4 now states what the code actually does with
  volunteer rejection notes.
- **Unchanged on purpose, flagged as open questions**: the volunteer rejection-notes
  contract (required by the validator, not stored — Q16 awaits a supervisor decision on a
  `decision_notes` column), the evidence upload lock timing (Q23), and the decision timing
  relative to the interview (Q24). No endpoint or DTO shape changed, so existing clients
  keep working.

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

### Migration script fixes

The two migration scripts failed on a database that really needed them, so they are fixed:

- `Hamzet_Wasl_Migration_v2_1_review_created_at.sql` referenced the column it was creating
  (`created_at`) in the same batch as the `ALTER TABLE`. T-SQL compiles a whole batch before
  running it, so the script died with *"Invalid column name 'created_at'"*. The statements are
  now split into separate batches (add → backfill → NOT NULL → default constraint), and each
  step is guarded so the script stays idempotent.
- Both scripts now start with `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;`. `sqlcmd` runs with
  `QUOTED_IDENTIFIER OFF`, and `VERIFICATION_REVIEW` carries a filtered index, so every write on
  that table failed with error 1934 (the transaction was rolled back).
- `tools/start-api.ps1` probes the database before starting the API and applies the two scripts
  automatically when their objects are missing, so a stale database no longer blocks the
  one-click start.

### Design alignment and consistency round (November 24, 2026)

No contract change: every request/response field and status code is unchanged.

- `serviceRadiusKm` is now **1–100 km** (was 1–200), matching the Figma location screen
  ("up to 100 km"). Recorded as `docs/DEVIATIONS.md` #24; existing databases apply
  `db/migrations/Hamzet_Wasl_Migration_v2_4_radius_100.sql`.
- Evidence retention: a file whose delete fails keeps its database row and is retried on
  the next run instead of being orphaned on disk (`EvidenceRetentionCleanupService`);
  `LocalFileStorage.DeleteAsync` now surfaces real IO failures so the retry can happen.
- The admin volunteer/organization decisions wrap the conditional update, the profile or
  organization update and the audit in **one transaction**, so a mid-way failure can no
  longer leave a decided review with a stale profile.
- Account anonymisation (`ResolveDeletionRequestAsync` approve path) runs in **one
  transaction**; the file deletions happen after the commit and are best-effort.
- Evidence replacement (`UploadEvidenceAsync`) swaps the rows in **one transaction** and
  removes the newly stored file again if the swap fails.
- The Google `registrationTicket` is now **verified** when a client sends it back during
  registration completion (same email and Google subject as the ID token); it was issued
  but never checked before. Clients that re-send only the ID token are unaffected.
- Replacing an organization document removes the old file only after the row points at
  the new one, and the old-file deletes of avatar/logo/cover replacement are best-effort.

### Earlier changes inside v1

- `POST /me/verification/evidence` (was documented as `PUT`): the evidence type is part of the
  multipart body (`evidenceType` + `file`).
- Admin decisions **require** `notes` when rejecting (`VALIDATION_ERROR` otherwise).
- Availability slots are validated as future, ordered and non-overlapping.
- Interview slots must start in the future; duration stays 15–120 minutes.
- Volunteer `weeklyHours` is validated as 1–168.
- `serviceRadiusKm` is optional (the schema allows `NULL`); when present it must be 1–100 km
  (1–200 before the design alignment round above).
