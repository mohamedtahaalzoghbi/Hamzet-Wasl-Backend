# ERD → API Map

How the 60 authoritative tables in `Hamzet_Wasl_Schema_v2.sql` (plus the Identity allowlist)
map to Phase-1 endpoints, services, and code. Full contracts live in `docs/API_CONTRACT.md`.

Legend:
- **CRUD** – Create/Read/Update via the listed endpoint(s).
- **intern** – read/written by internal services only; no direct endpoint yet (Phase 2 or workflow rows).
- **seed** – populated by `DatabaseSeeder` / lookup SQL only.

## Identity & accounts

| Table | Access | Endpoints / notes |
|---|---|---|
| `USER_ACCOUNT` | CRUD | register/login/session; admin suspension sets `account_status` |
| `AspNetUsers` (Identity allowlist) | intern | hashes, security stamp, lockout; 1:1 with `USER_ACCOUNT` |
| `AspNetUserTokens` (Identity allowlist) | intern | hashed rotating refresh tokens |
| `ROLE`, `USER_ROLE` | seed | 5 roles fixed; seeded once |
| `USER_PROFILE` | CRUD | `GET/PUT /me/profile`, `PUT /me/profile/avatar` |
| `USER_INTEREST`, `INTEREST` | CRUD / seed | `PUT /me/interests` (replace-set), `GET /meta/interests` |
| `USER_SKILL`, `SKILL` | CRUD / seed | `PUT /me/skills`, `GET /meta/skills` |
| `USER_REFERENCE` (social links) | CRUD | `PUT /me/verification/social-links` |
| `CONSENT_RECORD` | create | written at register; DELETE accessibility logs withdrawal |
| `DELETION_REQUEST` | CRUD | created by user flow, `GET /admin/deletion-requests` + resolve |
| `DEVICE_SESSION` | create/read | written during registration/login flows; admin-visible later |

## Accessibility (`disabled_user` only)

| Table | Access | Endpoint |
|---|---|---|
| `ACCESSIBILITY_PROFILE` | CRUD | `GET/PUT/DELETE /me/accessibility` |
| `ACCOMMODATION` | seed | `GET /meta/accommodations` |
| `USER_ACCOMMODATION` | CRUD | stored with accessibility upsert |
| `NEED_TYPE` | seed | `GET /meta/need-types` |

## Volunteers

| Table | Access | Endpoint |
|---|---|---|
| `VOLUNTEER_PROFILE` | CRUD | `GET/PUT /me/volunteer-profile` (also drives verification steps) |
| `VOLUNTEER_SKILL` | CRUD | included in volunteer-profile upsert |
| `VERIFICATION_REVIEW` | CRUD | `GET /me/verification`, POST/PUT/DELETE `/me/verification/interview`, admin reviews + decision |
| `VERIFICATION_EVIDENCE` | CRUD | `PUT /me/verification/evidence`; admin detail shows signed URLs |
| `INTERVIEW_SLOT` | CRUD | volunteer-public list; admin create/delete; booked transitionally |
| `TRAINING_MODULE`, `TRAINING_ATTEMPT` | read/CRUD | `GET /me/training`, `POST /me/training/{id}/attempts` |
| `VOLUNTEER_AVAILABILITY` | CRUD | `GET/PUT /me/availability` |

## Organizations

| Table | Access | Endpoints |
|---|---|---|
| `ORGANIZATION` | CRUD | `GET/PUT /me/organization`, logo/cover uploads |
| `ORGANIZATION_REVIEW` | CRUD | `POST /me/organization/submit`, `GET /me/organization/review-status`, admin queue + decision |
| `ORGANIZATION_DOCUMENT` | CRUD | `PUT /me/organization/documents/{type}`; admin detail shows signed URLs |
| `FIELD` (business fields) | seed | `GET /meta/business-fields` (keys validated against static lists) |

## Admin / platform

| Table | Access | Endpoints |
|---|---|---|
| `ADMIN_AUDIT` | create/read | written by every admin action; `GET /admin/audit` |
| `NOTIFICATION` | CRUD/intern | created on decisions; delivered via `/hubs/notifications` |
| `FEATURE_REGISTRY` | seed/intern | feature flags; consumption starts Phase 2 |

## Phase 2 tables (no endpoints yet)

These are created by the schema and enforced by business rules in
`HamzetWasl.Domain.Rules`, but the Phase-1 API does not expose them:
`DIRECTORY_ITEM`, `SERVICE_INQUIRY`, `PARENT_ORGANIZATION`, `PLACE`, `PLACE_FACILITY`,
`HELP_REQUEST`, `HELP_OFFER`, `HELP_ASSIGNMENT`, `HELP_MESSAGE`, `RATING`,
`JOB`, `JOB_APPLICATION`, `SAVED_JOB`, `RECRUITMENT_EVENT`, `EVENT_ATTENDEE`,
`POST`, `POST_MEDIA`, `COMMENT`, `COMMENT_LIKE`, `POST_REACTION`,
`CONTENT_REPORT`, `SAFETY_REPORT`, `SOS_ALERT`, `SOS_RESPONSE`,
`PHASE_REGISTRY`, `PHASE_FEATURE`, `USER_FOLLOW`, `BLOCKED_USER`, `USER_BLOCK`.

Their planned gating (already encoded in `BusinessRules`):
- `CanPublish` – organizations must be `approved` before posting directory items/jobs.
- `CanSubmitHelpOffer` – only `approved` volunteers.
- `CanOwnDirectoryItem` – associations only; `CanOwnJob` – companies only.
- Ratings are 1–5 with self-action blocked (`IsSelf`).
