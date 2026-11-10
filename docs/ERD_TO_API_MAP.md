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
| `USER_ACCOUNT` | CRUD | register/login/session; admin suspension sets `account_status`; role via `role_id` |
| `ACCOUNT_ROLE` | seed | the 5 fixed roles (`disabled_user`, `volunteer`, `association`, `company`, `admin`) |
| `AspNetUsers` (Identity allowlist) | intern | hashes, security stamp, lockout; 1:1 with `USER_ACCOUNT` |
| `AspNetUserTokens` (Identity allowlist) | intern | hashed rotating refresh tokens |
| `USER_PROFILE` | CRUD | `GET/PUT /me/profile`, `PUT /me/profile/avatar` |
| `USER_INTEREST`, `INTEREST` | CRUD / seed | `PUT /me/interests` (replace-set), `GET /meta/interests` |
| `USER_SKILL`, `SKILL` | CRUD / seed | `PUT /me/skills`, `GET /meta/skills` |
| `VOLUNTEER_SOCIAL_LINK` | CRUD | `PUT /me/verification/social-links` (volunteer social profiles) |
| `CONSENT_RECORD` | create | written at register; DELETE accessibility logs withdrawal |
| `DELETION_REQUEST` | CRUD | created by user flow, `GET /admin/deletion-requests` + resolve |
| `EMAIL_VERIFICATION`, `PHONE_VERIFICATION` | create/read | OTP lifecycle for `/auth/email/*` and `/auth/phone/*` |

## Accessibility (`disabled_user` only)

| Table | Access | Endpoint |
|---|---|---|
| `ACCESSIBILITY_PROFILE` | CRUD | `GET/PUT/DELETE /me/accessibility` |
| `ACCOMMODATION` | seed | `GET /meta/accommodations` |
| `NEED_TYPE` | seed | `GET /meta/need-types` |

## Volunteers

| Table | Access | Endpoint |
|---|---|---|
| `VOLUNTEER_PROFILE` | CRUD | `GET/PUT /me/volunteer-profile` (also drives verification steps) |
| `VOLUNTEER_SKILL` | CRUD | included in volunteer-profile upsert |
| `VERIFICATION_REVIEW` | CRUD | `GET /me/verification`, POST/PUT/DELETE `/me/verification/interview`, admin reviews + decision |
| `VERIFICATION_EVIDENCE` | CRUD | `POST /me/verification/evidence`; admin detail shows signed URLs |
| `INTERVIEW_SLOT` | CRUD | volunteer-public list; admin create/delete; booked transitionally |
| `TRAINING_MODULE`, `TRAINING_ATTEMPT` | read/CRUD | `GET /me/training`, `POST /me/training/{id}/attempts` |
| `AVAILABILITY_SLOT` | CRUD | `GET/PUT /me/availability` |

## Organizations

| Table | Access | Endpoints |
|---|---|---|
| `ORGANIZATION` | CRUD | `GET/PUT /me/organization`, logo/cover uploads |
| `ORGANIZATION_REVIEW` | CRUD | `POST /me/organization/submit`, `GET /me/organization/review-status`, admin queue + decision |
| `ORGANIZATION_DOCUMENT` | CRUD | `PUT /me/organization/documents/{type}`; admin detail shows signed URLs |

Business fields, employee ranges, governorates and areas are **not tables**: they are static
catalogues validated against `MetaCatalog` and served by `/meta/*`.

## Admin / platform

| Table | Access | Endpoints |
|---|---|---|
| `ADMIN_AUDIT` | create/read | written by every admin action; `GET /admin/audit` |
| `NOTIFICATION` | CRUD/intern | created on decisions; delivered via `/hubs/notifications` |
| `FEATURE_REGISTRY` | seed/intern | feature flags; consumption starts Phase 2 |
| `ASSISTANT_LOG` | intern | assistant usage log; no endpoint in Phase 1 |

## Phase 2 tables (no endpoints yet)

These 30 tables exist in the schema and their business rules already live in
`HamzetWasl.Domain.Rules`, but the Phase-1 API does not expose them:

`DIRECTORY_ITEM`, `SERVICE_INQUIRY`, `INQUIRY_MESSAGE`, `JOB`, `JOB_ACCOMMODATION`,
`JOB_APPLICATION`, `APPLICATION_STATUS_CHANGE`, `HELP_REQUEST`, `HELP_OFFER`, `HELP_ASSIGNMENT`,
`HELP_CONVERSATION`, `HELP_MESSAGE`, `HELP_FEEDBACK`, `SAFETY_REPORT`, `COMMUNITY`,
`COMMUNITY_MEMBERSHIP`, `USER_FOLLOW`, `POST`, `POST_MEDIA`, `POST_REACTION`, `COMMENT`,
`CONTENT_REPORT`, `PLACE`, `FACILITY_TYPE`, `PLACE_FACILITY`, `PLACE_REVIEW`, `OBSTACLE_REPORT`,
`EMERGENCY_CONTACT`, `SOS_ALERT`, `SOS_DELIVERY`.

Their planned gating (already encoded in `BusinessRules`):
- `CanPublish` – organizations must be `approved` before posting directory items/jobs.
- `CanSubmitHelpOffer` – only `approved` volunteers.
- `CanOwnDirectoryItem` – associations only; `CanOwnJob` – companies only.
- Ratings are 1–5 with self-action blocked (`IsSelf`).

## Table count check

The schema defines exactly 60 tables. The mapping above covers all of them, and
`SchemaMatchesSqlTests` (unit) plus `SchemaAppliedTests` (integration) fail the build if the
EF model or the applied database drifts from `Hamzet_Wasl_Schema_v2.sql`.
