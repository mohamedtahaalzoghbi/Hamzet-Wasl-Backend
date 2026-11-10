# Hamzet Wasl — API Contract (Phase 1)

The single source of truth for Flutter/Web clients consuming `https://<host>/api/v1`.
Everything here is implemented in Phase 1. Phase 2 endpoints follow the same rules and
will be added in their own sections when delivered.

- Machine-readable sibling: `docs/openapi.json` (exported from the running app; validated in CI).
- Conventions and assumptions live in `docs/OPEN_QUESTIONS.md`.
- Table-to-endpoint mapping lives in `docs/ERD_TO_API_MAP.md`.

## 1. Global conventions

| Concern | Rule |
|---|---|
| Base path | All endpoints live under `/api/v1` |
| Envelope | Every response (success and error) uses the envelope below |
| JSON | camelCase property names; enums as lowercase snake_case strings |
| IDs | GUIDs, lowercase hyphenated |
| Dates | ISO-8601 UTC (`2026-01-15T10:30:00Z`) |
| Phone | E.164 (`+201012345678`); input accepts `01xxxxxxxxx` and is normalised server-side |
| Pagination | `?page=1&pageSize=20` (default 1/20, max 100); paged payloads are `PagedResult` |
| Errors | Stable English `code` + Arabic human `message`; `fields` lists per-property violations |
| Auth | `Authorization: Bearer <accessToken>` |
| Clock | Values of `During - Requested` semantics are always server-authoritative (UTC) |
| Trace | Each response carries a `traceId` string (echo it in bug reports) |

### Success envelope

```json
{
  "success": true,
  "data": { },
  "error": null,
  "traceId": "0HN7..."
}
```

### Error envelope

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "بيانات الطلب غير صحيحة.",
    "fields": { "email": ["صيغة البريد الإلكتروني غير صحيحة."] }
  },
  "traceId": "0HN7..."
}
```

Status codes are conventional (200/201/204/400/401/403/404/409/413/422/429/500).
Clients must branch on `error.code`, never on `error.message` (messages are Arabic and may change).

### Paged payload

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 42 }
```

### HTTP status → error-code map (Phase 1 codes)

| code | Status | Meaning |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Request failed validation, `fields` populated |
| `UNAUTHORIZED` | 401 | Missing/expired access token |
| `FORBIDDEN` | 403 | Authenticated but role not permitted |
| `NOT_FOUND` | 404 | Resource missing or not owned by caller |
| `CONFLICT` | 409 | Generic conflict |
| `EMAIL_ALREADY_EXISTS` | 409 | Registration email taken |
| `PHONE_ALREADY_EXISTS` | 409 | Registration phone taken |
| `INVALID_CREDENTIALS` | 401 | Login failed |
| `ACCOUNT_SUSPENDED` | 403 | Admin suspended this account |
| `ACCOUNT_TYPE_REQUIRED` | 422 | Google sign-in needs role selection (`registrationTicket`) |
| `EMAIL_NOT_VERIFIED` / `PHONE_NOT_VERIFIED` | 403 | Action requires a verified channel first |
| `OTP_EXPIRED` / `OTP_INVALID` / `OTP_TOO_MANY_ATTEMPTS` / `OTP_RESEND_TOO_SOON` | 400/422 | Code lifecycle |
| `GOOGLE_NOT_CONFIGURED` / `GOOGLE_TOKEN_INVALID` / `GOOGLE_TICKET_INVALID` | 400/501 | Google flows |
| `VERIFICATION_INCOMPLETE` | 400 | Volunteer verification steps unfinished |
| `VERIFICATION_ALREADY_SUBMITTED` | 409 | Review already pending/decided |
| `ORGANIZATION_NOT_APPROVED` | 403 | Publishing Phase-2 features without approval |
| `DOCUMENTS_MISSING` | 400 | Organization submit lacks required documents |
| `PROFILE_INCOMPLETE` | 400 | Organization submit lacks complete profile |
| `DOCUMENT_TYPE_INVALID` | 422 | Unknown document type path segment |
| `ORGANIZATION_ALREADY_SUBMITTED` | 409 | Review already pending |
| `FILE_TOO_LARGE` | 413 | Upload over category limit |
| `FILE_TYPE_NOT_ALLOWED` | 415 | Content/magic bytes rejected |
| `INTERVIEW_SLOT_UNAVAILABLE` | 409 | Slot taken/cancelled/past |
| `INTERVIEW_ALREADY_BOOKED` | 409 | Volunteer holds an active booking |
| `INVALID_STATE` | 409 | Transition not allowed in current state |
| `SELF_ACTION_NOT_ALLOWED` | 400 | E.g. admin acting on own account |
| `ACCESSIBILITY_NOT_ALLOWED` | 403 | Only `disabled_user` may own accessibility settings |
| `CONSENT_REQUIRED` | 422 | Terms/privacy not accepted |
| `REQUEST_TOO_LARGE` | 413 | Body over limit |
| `RATE_LIMITED` | 429 | Rate limit hit; includes `Retry-After` |
| `INTERNAL_ERROR` | 500 | Unexpected; traceId for support |

## 2. Authentication

Roles: `disabled_user`, `volunteer`, `association`, `company`, `admin`.
`admin` is never publicly registrable (seeded). Access tokens live 15 minutes, refresh tokens 30 days,
are single-use (rotation on every `/auth/refresh`), stored server-side hashed.

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/auth/register` | – | Public registration for the 4 public roles |
| POST | `/auth/login` | – | Email or phone + password |
| POST | `/auth/google` | – | Google ID token sign-in / registration completion |
| POST | `/auth/refresh` | – | Rotate refresh token → new pair |
| POST | `/auth/logout` | Bearer | Revoke supplied refresh token |
| POST | `/auth/forgot-password` | – | Always 204 (never reveals existence) |
| POST | `/auth/reset-password` | – | Email + `token` + new password |
| POST | `/auth/change-password` | Bearer | Current + new password |
| POST | `/auth/email/send-code` \| `/auth/email/verify` | Bearer | 6-digit email OTP |
| POST | `/auth/phone/send-code` \| `/auth/phone/verify` | Bearer | 6-digit phone OTP |
| GET | `/auth/session` | Bearer | Current `UserSession` |

`POST /auth/register` body:

```json
{
  "role": "volunteer",
  "name": "أحمد",                    // personal roles: disabled_user, volunteer
  "organizationName": "شركة أمجاد",  // organization roles: association, company
  "email": "user@example.com",
  "phone": "01012345678",
  "password": "Passw0rd!",
  "confirmPassword": "Passw0rd!",
  "termsAccepted": true,
  "policyVersion": "1.0"
}
```

`201` → `Auth` (see `/auth/login`). Failed: 409 `EMAIL_ALREADY_EXISTS` / `PHONE_ALREADY_EXISTS`,
400 `VALIDATION_ERROR`, 422 `CONSENT_REQUIRED`.

`POST /auth/login` body `{ "emailOrPhone": "user@example.com", "password": "Passw0rd!" }` →
`200 Auth`. Failed: 401 `INVALID_CREDENTIALS` or 403 `ACCOUNT_SUSPENDED`.

`Auth` payload (both register and login):

```json
{
  "userId": "…",
  "role": "volunteer",
  "accountStatus": "active",
  "email": "user@example.com",
  "emailVerified": false,
  "phone": "+201012345678",
  "phoneVerified": false,
  "displayName": "أحمد",
  "accessToken": "…",
  "accessTokenExpiresAt": "2026-01-15T10:45:00Z",
  "refreshToken": "…",
  "refreshTokenExpiresAt": "2026-02-14T10:30:00Z"
}
```

Registering organization accounts (`association`, `company`) creates the organization row with
`approval_status='pending'`. Their `accountStatus` starts as `pending_approval`.

## 3. Meta (public reference data)

| Method | Path | Returns |
|---|---|---|
| GET | `/meta/account-types` | `[{key, nameAr, descriptionAr}]` |
| GET | `/meta/governorates` | `[{key, nameAr, areas:[{key,nameAr}]}]` |
| GET | `/meta/governorates/{key}/areas` | `[{key, nameAr}]` |
| GET | `/meta/business-fields` | `[{key, nameAr}]` |
| GET | `/meta/employee-ranges` | `[{key, nameAr}]` |
| GET | `/meta/skills` | `[{id, key, nameAr}]` |
| GET | `/meta/interests` | `[{id, key, nameAr}]` |
| GET | `/meta/need-types` | `[{id, key, nameAr, descriptionAr}]` (visual, motor, hearing) |
| GET | `/meta/accommodations` | `[{id, key, nameAr, category}]` |

## 4. Profile (`RoleNames` any authenticated user)

| Method | Path | Description |
|---|---|---|
| GET | `/me/profile` | Own profile |
| PUT | `/me/profile` | `{displayName, bio, city, visibility}` (`public|followers|private`) |
| PUT | `/me/profile/avatar` | multipart `file` (PNG/JPG ≤ 5 MB); returns profile with `avatarUrl` |
| PUT | `/me/interests` | `{interestIds:[int]}` replaces the set |
| PUT | `/me/skills` | `{skillIds:[int]}` replaces the set |

Note: skills are the volunteer competency list; organizations use `business-field`.

## 5. Accessibility profile (`disabled_user` only)

| Method | Path | Description |
|---|---|---|
| GET | `/me/accessibility` | Current settings or 404 `NOT_FOUND` if never created |
| PUT | `/me/accessibility` | Upsert (creates on first call) |
| DELETE | `/me/accessibility` | Consent withdrawal; deletes rows and resets need_type/overrides |

`PUT` body:

```json
{
  "needType": "visual",              // visual | motor | hearing — exactly one
  "theme": "dark",                   // system | light | dark | high_contrast
  "fontScale": 1.25,                 // 1.0 – 2.0
  "reducedMotion": true,
  "interactionMode": "screen_reader",// touch | keyboard | screen_reader | voice
  "notificationMode": "visual_alert",// in_app | push | visual_alert | vibration
  "manualOverrides": { "any": "json" } // optional; full preset override, client-owned
}
```

Response `Accessibility` adds `updatedAt` and `effectivePreset` — the computed active preset the
client should render (manual overrides win over need-type defaults). Non-`disabled_user` callers
get 403 `ACCESSIBILITY_NOT_ALLOWED`.

## 6. Volunteer

| Method | Path | Description |
|---|---|---|
| GET/PUT | `/me/volunteer-profile` | `{introduction, helpMode, skillLevel, weeklyHours, experienceNotes, skillIds}` |
| GET/PUT | `/me/volunteer-location` | `{governorate, serviceArea, latitude?, longitude?, serviceRadiusKm∈[1,200]}` |
| GET | `/me/verification` | `Verification` status object (drives the verification wizard UI) |
| PUT | `/me/verification/evidence` | single evidence upload (multipart, images ≤ 10 MB) |
| PUT | `/me/verification/social-links` | `{links:[{platform,url}]}`; `platform-origin` verified |
| GET | `/interview-slots` | public list of `available` slots (`[{interviewSlotId, startsAt, durationMinutes, status}]`) |
| POST | `/me/verification/interview` | `{interviewSlotId}` — book atomically |
| PUT | `/me/verification/interview` | change booking (blocked once admin approved) |
| DELETE | `/me/verification/interview` | cancel booking (frees slot; blocked once approved) |
| PUT | `/me/availability` | `{slots:[{startsAt, endsAt, timezone}]}` — ordered, non-overlapping, future |
| GET | `/me/availability` | own availability |
| GET | `/me/training` | `{moduleId, title, version, attempts:[{attemptId, score, result, completedAt}]}` |
| POST | `/me/training/{moduleId}/attempts` | `{score 0–100}`; ≥ 70 = `passed` |

`Verification` shape:

```json
{
  "status": "interview_scheduled",
  "currentStep": "interview",
  "reviewId": "…",
  "interviewAt": "2026-01-20T12:00:00Z",
  "canSubmit": true,
  "canBookInterview": true,
  "steps": [{ "key": "profile", "titleAr": "…", "completed": true, "required": true }],
  "uploadedEvidenceTypes": ["id_card"],
  "socialLinks": [{ "platform": "linkedin", "url": "https://…" }]
}
```

Lifecycle: `unverified` → fill profile + evidence + social links → `pending` → book interview
→ `interview_scheduled` → admin `approve` → `approved` (can submit Phase-2 help offers), or `rejected`.
Rejection is silent to volunteers (no `decision_notes` in `VERIFICATION_REVIEW`; audit only).

## 7. Organization (`association` and `company` only)

| Method | Path | Description |
|---|---|---|
| GET | `/me/organization` | Current organization profile (404 for non-org accounts) |
| PUT | `/me/organization` | `{name, field, description, governorate, city, website?, employeeCountRange, contactInformation}` |
| PUT | `/me/organization/logo` | multipart image (PNG/JPG ≤ 5 MB) |
| PUT | `/me/organization/cover` | multipart image (PNG/JPG ≤ 5 MB) |
| PUT | `/me/organization/documents/{documentType}` | `commercial_register` \| `tax_card` \| `address_proof` (PDF/JPG/PNG ≤ 10 MB); `license` optional |
| POST | `/me/organization/submit` | Create/refresh admin review |
| GET | `/me/organization/review-status` | `{approvalStatus, reviewId?, reviewStatus?, requestedAt?, decidedAt?, decisionNotes?, missingDocuments[], canSubmit}` |

Rules:
- `documents` returns `[ {documentType, uploadedAt} ]` (no keys or URLs).
- `canSubmit` is true only when the profile is complete AND all three required documents exist.
- Submit with gaps → 400 `PROFILE_INCOMPLETE` or `DOCUMENTS_MISSING` (never partial state).
- Rejected organizations stay `pending_approval` and may edit + resubmit; each submit creates a new review.
- On approval `approval_status='approved'`, `account_status='active'`, and the organization receives a notification.

## 8. Admin (`admin` only)

| Method | Path | Description |
|---|---|---|
| GET | `/admin/volunteer-reviews` | paged; filter `?status=` |
| GET | `/admin/volunteer-reviews/{reviewId}` | detail incl. evidence short-lived URLs |
| POST | `/admin/volunteer-reviews/{reviewId}/decision` | `{approve, notes?}` (approve doesn't need notes) |
| GET | `/admin/organization-reviews` | paged; filter `?status=` |
| GET | `/admin/organization-reviews/{reviewId}` | detail incl. documents short-lived URLs |
| POST | `/admin/organization-reviews/{reviewId}/decision` | `{approve, notes?}` |
| GET/POST | `/admin/interview-slots` | list / create `{startsAt, durationMinutes}` |
| DELETE | `/admin/interview-slots/{slotId}` | free slot (available + unbooked only) |
| PUT | `/admin/users/{userId}/suspension` | `{suspend, reason?}`; org suspension restores `pending_approval` when applicable |
| GET | `/admin/deletion-requests` | paged queue |
| POST | `/admin/deletion-requests/{id}/resolve` | `{approve, notes?}`; approve runs anonymisation |
| GET | `/admin/audit` | paged audit trail `[ {auditId, actorUserId, action, targetType, targetId, outcome, occurredAt} ]` |

Reviewer evidence/document URLs are short-lived signed URLs (15-minute expiry by default).
All decisions are written to `ADMIN_AUDIT`.

## 9. Files

| Method | Path | Description |
|---|---|---|
| GET | `/files/{*key}?expires={unix}&signature={hmac}` | Validates HMAC; 302 or streams the content |

Direct media (`StoredFile.publicUrl`, e.g. `/media/public/logos/…`) is only for public assets
(logos, covers, avatars). Evidence and organization documents are **private** — they always require
signature + expiry and are stored outside the web root.

## 10. Real-time notifications (SignalR)

- Route: `/hubs/notifications`
- Auth: send `Authorization: Bearer <accessToken>` on negotiate; unknown tokens are rejected.
- Client method: `notificationReceived(NOTIFICATION)` — same shape as the data model notification
  (`id`, `type`, `titleAr`, `bodyAr`, `createdAt`, `payloadJson`).
- Emitted on: organization approval/rejection, volunteer verification approval/rejection,
  interview booked/changed/cancelled, suspension, admin decisions.

## 11. Non-functional guarantees

- **No PII in logs**: Serilog templates never include emails, phones, tokens, or evidence keys.
- **Envelope consistency**: even 5xx responses pass through the exception middleware and use the
  standard envelope with `INTERNAL_ERROR`.
- **Idempotent lookups**: GET endpoints are side-effect free; PUT upserts replace-entire-set semantics.
- **Localization**: `message` is always Arabic (Egyptian); `code` is stable ASCII.
- **OpenAPI**: served at `/swagger/v1/swagger.json`; exported copy committed to `docs/openapi.json`
  with a CI drift check.
