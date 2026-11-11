# Hamzet Wasl — Open Questions and Assumptions

This file records unresolved decisions. Code that depends on a default below is marked with `// ASSUMPTION:`.

## A. Blocking decisions

### Q1 — Identity mapping

`Hamzet_Wasl_Schema_v2.sql` says password hashes are **not** stored in `USER_ACCOUNT`, and the master prompt allows either extending `IdentityUser<Guid>` or linking 1:1.

**Default used:** `AspNetUsers` (ASP.NET Core Identity tables) is the authentication table. `USER_ACCOUNT` remains the exact domain table. They share `user_id` and are linked logically (and by an FK from `AspNetUsers.Id` to `USER_ACCOUNT.user_id`, an Identity-only table). Email, phone, and verified flags are synchronised by the account service.

**Consequence:** The schema-comparison test covers the 62 domain tables only. Identity tables are explicitly allowlisted.

### Q2 — Refresh tokens and OTP throttling

There is no table or column for refresh tokens, OTP attempt counts, or resend timestamps.

**Default used:** Hashed refresh tokens are stored in `AspNetUserTokens` (Identity table) as JSON. OTP throttling and attempt counters use `IDistributedCache` (in-memory in development, Redis in production). ASP.NET Core rate limiting protects auth/OTP endpoints.

### Q3 — Schema test scope

The EF model includes Identity tables that are not part of the 62-table SQL script.

**Default used:** **No EF Core migrations are used.** `Hamzet_Wasl_Schema_v2.sql` is the single
source of truth: it is embedded in `HamzetWasl.Infrastructure` and applied by `SchemaBootstrapper`
whenever the domain tables are missing. Two tests guard the parity:

- `SchemaMatchesSqlTests` (unit) compares the EF store model with the parsed script for all 60
  tables: table set, columns (name, type, nullability), primary keys, foreign keys, unique
  constraints and indexes. `AspNet*` tables are allowlisted; the EF model must not declare a
  CHECK constraint that the script does not contain.
- `SchemaAppliedTests` (integration) compares the **applied database** with the script: the 62
  tables, the CHECK constraints (by the values they allow), the 86 foreign keys, the unique
  constraints and the 69 indexes.

`AspNet*` tables and `__EFMigrationsHistory` are ignored by the comparison.

### Q4 — Missing enum values

Many status columns have no SQL `CHECK`. Proposed values are in §C below.

**Default used:** The values in §C are application enums and are serialised as lowercase snake_case strings.

### Q5 — Multiple disability types

The master prompt says exactly one `need_type_id`; the Figma export says “one or more”.

**Default used:** Exactly one primary `need_type_id` in the table. The API accepts up to three
needs through the optional `needTypes` list of `PUT /me/accessibility` (the first must equal
`needType`); the extra needs are stored in `manual_overrides.needTypes` (`["visual","hearing"]`),
and the calculated `effectivePreset` merges the need-type defaults without overwriting the user's
own preferences (see `docs/API_CONTRACT.md` §5). The client can therefore use a multi-select of up
to three needs.

### Q6 — OTP length

The Figma export shows a 4-digit placeholder; the master prompt says 6-digit SMS OTP.

**Default used:** 6 digits for both email and phone.

### Q7 — Endpoint gaps

The master prompt names capabilities without exact endpoints: training, availability, deletion requests, notifications, volunteer profile, and meta lists for skills/interests/need-types/accommodations.

**Default used:** Minimal REST endpoints under `/api/v1/me`, `/api/v1/admin`, and `/api/v1/meta`. They add no new product features. Each endpoint is listed in `docs/API_CONTRACT.md`.

### Q8 — Phase 1 / Phase 2 delivery

**Default used:** Phase 1 is implemented, built, and tested first. Phase 2 starts only after Phase 1 review.

### Q9 — Organization documents

**Default used:** Both company and association must upload `commercial_register`, `tax_card`, and `address_proof` before submit. `license` is optional.

### Q10 — Reference data

There are no tables for governorates, areas, business fields, or employee ranges.

**Default used:** Static metadata in `HamzetWasl.Application` / configuration. The free-text columns `ORGANIZATION.field`, `ORGANIZATION.employee_count_range`, `ORGANIZATION.governorate`, `ORGANIZATION.city`, `VOLUNTEER_PROFILE.governorate`, and `VOLUNTEER_PROFILE.service_area` are validated against those static lists.

### Q11 — Help matching without request coordinates

`HELP_REQUEST` has only `approximate_area`, with no latitude/longitude.

**Default used:** Phase 2 matches by `approximate_area` text, skill, availability, and urgency. No radius filter is applied. A static area-centroid lookup can be added later without schema changes.

### Q12 — Reassignment after volunteer decline

`HELP_ASSIGNMENT` has PK `request_id`, so only one assignment row per request is allowed.

**Default used:** An assignment is final. If the volunteer declines, the request becomes `cancelled` and the requester creates a new request. No history table is added.

### Q13 — Retention

**Default used:** Verification evidence: 90 days after decision. Organization documents: while the organization is active, plus 30 days after deletion. SOS alerts: 30 days. Audit records: 5 years. All configurable.

### Q14 — Rejected organization account

`USER_ACCOUNT.account_status` has no `rejected` value.

**Default used:** Keep `account_status='pending_approval'`; set `ORGANIZATION.approval_status='rejected'`; allow resubmission. `suspended` is reserved for policy violations.

### Q15 — First-time Google sign-in

**Default used:** `POST /api/v1/auth/google` with `idToken`. If the user does not exist and no role is supplied, return 422 `ACCOUNT_TYPE_REQUIRED` with a short-lived signed registration ticket. A second call with role, phone, and terms completes registration.

### Q16 — Volunteer rejection notes

`VERIFICATION_REVIEW` has no `decision_notes` column.

**Default used:** No free-text volunteer rejection notes. The decision is recorded in `VERIFICATION_REVIEW.status` and `ADMIN_AUDIT` action/outcome.

### Q17 — Upload limits

**Default used:** Logo/cover/avatar images: PNG/JPG up to 5 MB. Company documents: PDF/PNG/JPG up to 10 MB. Verification evidence images: PNG/JPG up to 10 MB. Audio: up to 20 MB. Video: up to 100 MB. All configurable.

### Q18 — Seed content

**Default used:** Starter lists for skills, interests, accommodations, governorates, and areas. They can be replaced before release.

### Q19 — `ADMIN_AUDIT.target_id`

**Default used:** Phase 1 audits only GUID targets. Phase 2 non-GUID targets are excluded unless a deterministic GUID mapping is approved.

### Q20 — CI

**Default used:** LocalDB for local integration tests; SQL Server service container in GitHub Actions.

### Q21 — Creating a deletion request

**Resolved (Phase 1, post-review).** `POST /api/v1/me/deletion-request` creates the `pending`
row for the caller and answers 201 with `{deletionRequestId, status, requestedAt}`; a second call
while one is pending answers 409 `DELETION_REQUEST_ALREADY_PENDING`. The admin side (queue +
resolve with anonymisation) already existed, so the queue is now reachable end-to-end.

**Not exposed:** a withdraw/cancel endpoint (`DELETE /me/deletion-request/{id}`) because the
Figma has no such action. The filtered unique index that would close the concurrent-duplicate
race is deferred to the schema batch.

### Q22 — Listing the notifications

**Resolved (Phase 1, post-review).** `GET /api/v1/me/notifications?page=1&pageSize=20` returns
the caller's notifications newest first (paged, `pageSize` clamped to 1..100) and
`PUT /api/v1/me/notifications/{id}/read` writes `NOTIFICATION.read_at` (idempotent, 404 for an
id that belongs to another user). `read_at` is therefore no longer dead.

**Known limitation:** `payloadJson` (the notification target) is only carried by the live SignalR
event because the table has no column for it; see `docs/DEVIATIONS.md` entry 19.

## B. Assumptions

1. .NET 10.0.401, ASP.NET Core, EF Core 10, SQL Server, ASP.NET Core Identity, JWT bearer, FluentValidation, Serilog, Swashbuckle, SignalR, xUnit.
2. Clean Architecture layout exactly as specified in the master prompt.
3. `AspNetUsers` is 1:1 with `USER_ACCOUNT` on shared `user_id`; `USER_ACCOUNT` is unchanged.
4. Role authorization uses `USER_ACCOUNT.role_id`; JWT carries `role` and `accountStatus`; `admin` is seeded from environment variables.
5. Exactly one `need_type_id`; only `disabled_user` has `ACCESSIBILITY_PROFILE`; profile is created only when the user chooses; consent withdrawal deletes/resets it.
6. `ORGANIZATION` is created at registration with `approval_status='pending'`; `ORGANIZATION_REVIEW` is created only on submit; resubmission creates a new review.
7. A volunteer account stays `active` while verification proceeds; only fully approved volunteers can submit `HELP_OFFER`.
8. Interview booking is transactional; one slot ↔ at most one review; changing/cancelling frees the old slot.
9. Help request statuses are the SQL `CHECK` values; offer/assignment statuses use the proposed enums.
10. Jobs are published on creation; there is no draft state because `JOB.published_at` is `NOT NULL`.
11. Public DTOs never return other users' phone, email, exact coordinates, evidence keys, or ID images.
12. OTP: 6 digits, 10-minute TTL, 5 attempts, 60-second resend cooldown, hashed at rest.
13. Phone numbers are stored in E.164; `01xxxxxxxxx` becomes `+201xxxxxxxxx`.
14. All timestamps are UTC in `DATETIME2(0)`; display and slot semantics are `Africa/Cairo`.
15. Arabic is the default error language; stable English `code` values are always present.
16. `openapi.json` is exported into `docs/` and checked in CI.
17. No new tables, columns, or product features. Any gap is recorded here first.

## C. Proposed enums for columns without SQL CHECK

| Table.column | Values |
|---|---|
| `VOLUNTEER_PROFILE.verification_status` | `unverified`, `pending`, `interview_scheduled`, `approved`, `rejected`, `suspended` |
| `VERIFICATION_REVIEW.status` | `draft`, `pending`, `interview_scheduled`, `approved`, `rejected`, `cancelled` |
| `VERIFICATION_REVIEW.verification_level` | `basic`, `field` |
| `TRAINING_ATTEMPT.result` | `passed`, `failed` |
| `ORGANIZATION_REVIEW.status` | `pending`, `approved`, `rejected` |
| `USER_PROFILE.visibility` | `public`, `followers`, `private` |
| `ACCESSIBILITY_PROFILE.theme` | `system`, `light`, `dark`, `high_contrast` |
| `ACCESSIBILITY_PROFILE.interaction_mode` | `touch`, `keyboard`, `screen_reader`, `voice` |
| `ACCESSIBILITY_PROFILE.notification_mode` | `in_app`, `push`, `visual_alert`, `vibration` |
| `DELETION_REQUEST.status` | `pending`, `approved`, `rejected`, `completed` |
| `NOTIFICATION.notification_type` | `organization_approved`, `organization_rejected`, `verification_approved`, `verification_rejected`, `interview_booked`, `interview_changed`, `interview_cancelled` |
| `FEATURE_REGISTRY.implementation_status` | `implemented`, `in_progress`, `planned`, `disabled` |
| `ORGANIZATION.field` | `technology`, `education`, `health`, `marketing`, `finance`, `engineering`, `other` |
| `ORGANIZATION.employee_count_range` | `1-10`, `11-50`, `51-200`, `200+` |
| `HELP_OFFER.status` | `pending`, `accepted`, `rejected`, `withdrawn`, `expired` |
| `HELP_ASSIGNMENT.status` | `pending_volunteer`, `active`, `completed`, `cancelled` |
| `SAFETY_REPORT.status` | `open`, `under_review`, `resolved`, `dismissed` |
| `JOB.status` | `published`, `closed`, `expired` |
| `JOB_APPLICATION.status` | `submitted`, `under_review`, `shortlisted`, `interview`, `offer`, `rejected`, `withdrawn` |
| `DIRECTORY_ITEM.publication_status` | `draft`, `published`, `archived` |
| `SERVICE_INQUIRY.status` | `open`, `replied`, `closed` |
| `POST.moderation_status` / `COMMENT.moderation_status` | `pending`, `approved`, `hidden`, `rejected` |
| `CONTENT_REPORT.status` | `open`, `under_review`, `resolved`, `dismissed` |
| `PLACE_FACILITY.availability_status` | `available`, `unavailable`, `unknown` |
| `OBSTACLE_REPORT.status` | `open`, `verified`, `resolved`, `rejected` |
| `SOS_DELIVERY.delivery_status` | `pending`, `sent`, `delivered`, `failed` |
| `POST_MEDIA.media_type` | `image`, `audio`, `video` |
| `POST_REACTION.reaction_type` | `like`, `support`, `celebrate` |
