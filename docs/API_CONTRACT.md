# Hamzet Wasl — API Contract (Phase 1)

The single source of truth for Flutter/Web clients consuming `https://<host>/api/v1`.
Everything here is implemented in Phase 1 and verified against the running API.
Phase 2 endpoints follow the same rules and will be added in their own sections when delivered.

- Machine-readable sibling: `docs/openapi.json` (exported from the running app; validated in CI).
- Conventions and assumptions live in `docs/OPEN_QUESTIONS.md`.
- Table-to-endpoint mapping lives in `docs/ERD_TO_API_MAP.md`.
- The deviations from the master prompt live in `docs/DEVIATIONS.md`.
- The change history lives in `docs/CHANGELOG.md`.

## 1. Global conventions

| Concern | Rule |
|---|---|
| Base path | All endpoints live under `/api/v1` |
| Envelope | Every JSON response (success and error) uses the envelope below |
| JSON | camelCase property names; enums as lowercase snake_case strings |
| IDs | GUIDs, lowercase hyphenated |
| Dates | ISO-8601 UTC with the `Z` suffix (`2026-01-15T10:30:00Z`) — every timestamp, wherever it comes from |
| Phone | E.164 (`+201012345678`); input accepts `01xxxxxxxxx` and is normalised server-side |
| Pagination | `?page=1&pageSize=20` (default 1/20, max 100); paged payloads are `PagedResult` |
| Errors | Stable English `code` + Arabic human `message`; `fields` lists per-property violations |
| Auth | `Authorization: Bearer <accessToken>` |
| Clock | All timestamps are produced by the server in UTC; client-supplied times are validated, never trusted |
| Trace | Each response carries a `traceId` string (echo it in bug reports) |
| No content | The endpoints marked `204` answer with an **empty body** (no envelope) |
| Files | The file endpoint answers with the binary content, not with the envelope |

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

Only the codes the API really produces are listed.

| code | Status | Meaning |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Request failed validation, `fields` populated |
| `UNAUTHORIZED` | 401 | Missing/expired access token, wrong credentials, locked account |
| `FORBIDDEN` | 403 | Authenticated but role not permitted |
| `NOT_FOUND` | 404 | Resource missing or not owned by caller |
| `CONFLICT` | 409 | Generic conflict |
| `EMAIL_ALREADY_EXISTS` | 409 | Registration email taken |
| `PHONE_ALREADY_EXISTS` | 409 | The phone number is already used by another account |
| `INVALID_CREDENTIALS` | 401 | Login failed (unknown address, wrong password or locked account) |
| `ACCOUNT_SUSPENDED` | 403 | Admin suspended this account |
| `EMAIL_NOT_VERIFIED` / `PHONE_NOT_VERIFIED` | 422 | Action requires a verified channel first |
| `OTP_EXPIRED` | 422 | The code expired |
| `OTP_INVALID` | 400/422 | The code is wrong, or the password reset token is invalid |
| `OTP_TOO_MANY_ATTEMPTS` | 422 | Too many wrong codes |
| `OTP_RESEND_TOO_SOON` | 429 | Cooldown between two codes |
| `GOOGLE_NOT_CONFIGURED` | 503 | Google client id is not configured |
| `GOOGLE_TOKEN_INVALID` | 401 | Google id token rejected |
| `VERIFICATION_INCOMPLETE` | 422 | Volunteer verification steps unfinished |
| `DOCUMENTS_MISSING` | 422 | Organization submit lacks required documents |
| `PROFILE_INCOMPLETE` | 422 | Organization submit lacks a complete profile (the message lists what is missing) |
| `DOCUMENT_TYPE_INVALID` | 400 | Unknown document type path segment |
| `ORGANIZATION_ALREADY_SUBMITTED` | 409 | A review is already pending |
| `DELETION_REQUEST_ALREADY_PENDING` | 409 | The caller already has a pending account-deletion request |
| `FILE_TOO_LARGE` | 413 | The uploaded file is bigger than the limit of its category |
| `REQUEST_TOO_LARGE` | 413 | The request itself is bigger than the limit of the endpoint |
| `FILE_TYPE_NOT_ALLOWED` | 400 | Extension, content type or magic bytes rejected |
| `INTERVIEW_SLOT_UNAVAILABLE` | 409 | Slot taken/cancelled/past |
| `INTERVIEW_ALREADY_BOOKED` | 409 | Volunteer holds an active booking |
| `INVALID_STATE` | 422 | Transition not allowed in the current state |
| `SELF_ACTION_NOT_ALLOWED` | 422 | E.g. admin acting on own account |
| `ACCESSIBILITY_NOT_ALLOWED` | 403 | Only `disabled_user` may own accessibility settings |
| `RATE_LIMITED` | 429 | Rate limit hit; the answer carries a `Retry-After` header |
| `INTERNAL_ERROR` | 500 | Unexpected; traceId for support |

## 2. Authentication

Roles: `disabled_user`, `volunteer`, `association`, `company`, `admin`.
`admin` is never publicly registrable (seeded). Access tokens live 15 minutes, refresh tokens 30 days,
are single-use (rotation on every `/auth/refresh`), stored server-side hashed.

Only an **access token** authenticates a request: a refresh token or a Google registration ticket
is rejected with 401, even if it is sent as a bearer token.

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/auth/register` | – | Public registration for the 4 public roles (201) |
| POST | `/auth/login` | – | Email or phone + password (200) |
| POST | `/auth/google` | – | Google ID token sign-in (200); a first-time user gets `registrationRequired` in the payload |
| POST | `/auth/refresh` | – | Rotate refresh token → new pair (200) |
| POST | `/auth/logout` | Bearer | Revoke the supplied refresh token (204) |
| POST | `/auth/forgot-password` | – | Always 204 (never reveals existence) |
| POST | `/auth/reset-password` | – | Email + `token` + new password (204) |
| POST | `/auth/change-password` | Bearer | Current + new password (204) |
| POST | `/auth/email/send-code` \| `/auth/email/verify` | Bearer | 6-digit email OTP (204) |
| POST | `/auth/phone/send-code` \| `/auth/phone/verify` | Bearer | 6-digit phone OTP (204) |
| GET | `/auth/session` | Bearer | Current `UserSession` (200) |

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
400 `VALIDATION_ERROR`.

`POST /auth/login` body `{ "emailOrPhone": "user@example.com", "password": "Passw0rd!" }` →
`200 Auth`. Failed: 401 `INVALID_CREDENTIALS` (unknown address, wrong password **or a locked
account**) or 403 `ACCOUNT_SUSPENDED`. After five failed attempts the account is locked for
15 minutes.

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

### Phone verification and change

`POST /auth/phone/send-code` sends a code to the number given in the body. The number stays
**pending** until the code is verified: the account keeps the number it owns and no other
account can take a number over. `POST /auth/phone/verify` applies the number (409
`PHONE_ALREADY_EXISTS` if another account owns it). One code per number per minute.

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
| GET | `/me/profile` | Own profile (200) |
| PUT | `/me/profile` | `{displayName, bio, city, visibility}` (`public|followers|private`) (200) |
| PUT | `/me/profile/avatar` | multipart `file` (PNG/JPG ≤ 5 MB) → `{ "avatarUrl": "…" }` (200) |
| PUT | `/me/interests` | `{interestIds:[int]}` replaces the set (204) |
| PUT | `/me/skills` | `{skillIds:[int]}` replaces the set (204) |
| POST | `/me/deletion-request` | Creates the caller's `pending` deletion request; 201 with `{deletionRequestId, status, requestedAt}`; 409 `DELETION_REQUEST_ALREADY_PENDING` while one is pending |
| GET | `/me/notifications` | Notification centre, paged and newest first (`?page=1&pageSize=20`, size clamped to 1..100) (200) |
| PUT | `/me/notifications/{id}/read` | Marks one notification read; idempotent (204); 404 when the id is unknown or belongs to another user |

Note: skills are the volunteer competency list; organizations use `business-field`.

The deletion request only **asks** for deletion: the account stays usable until an admin approves
it and the anonymisation runs (`POST /admin/deletion-requests/{id}/resolve`). There is no
withdrawal endpoint in Phase 1 because the Figma has no such action.

## 5. Accessibility profile (`disabled_user` only)

| Method | Path | Description |
|---|---|---|
| GET | `/me/accessibility` | Current settings (200) or 404 `NOT_FOUND` if never created |
| PUT | `/me/accessibility` | Upsert (creates on first call) (200) |
| DELETE | `/me/accessibility` | Consent withdrawal; deletes the row and resets the need type (204) |

`PUT` body:

```json
{
  "needType": "visual",              // visual | motor | hearing — exactly one
  "theme": "system",                  // system | light | dark | high_contrast
  "fontScale": 1.0,                  // 1.0 – 2.0
  "reducedMotion": false,
  "interactionMode": "touch",        // touch | keyboard | screen_reader | voice
  "notificationMode": "in_app",      // in_app | push | visual_alert | vibration
  "manualOverrides": { "any": "json" } // optional; full preset override, client-owned
}
```

Response `Accessibility` adds `updatedAt` and `effectivePreset` — the **calculated** preset the
client should render.

### How `effectivePreset` is calculated

Precedence: **manual overrides → the user's own preferences → need-type defaults → neutral defaults**.

| Need type | Theme | Font scale | Reduced motion | Interaction | Notification |
|---|---|---|---|---|---|
| `visual` | `high_contrast` | 1.25 | false | `screen_reader` | `visual_alert` |
| `hearing` | `dark` | 1.0 | false | `touch` | `visual_alert` |
| `motor` | `light` | 1.0 | true | `keyboard` | `vibration` |

- A value the user changed away from the neutral baseline is always kept.
- A value the user never chose (still `system` / `1.0` / `false` / `touch` / `in_app`) is filled
  from the need type.
- **Several needs at once**: put them in `manualOverrides.needTypes`
  (`["visual","hearing"]`). The declared `needType` is applied first and the first need type
  that suggests a value wins; the other needs only fill the values the first one leaves open.
  The user's own preferences still win over the need types.
- `manualOverrides` may set `theme`, `fontScale`, `reducedMotion`, `interactionMode`,
  `notificationMode`, `highContrast` and `textDirection` directly; it has the highest priority.

`effectivePreset` = `{ theme, fontScale, reducedMotion, interactionMode, notificationMode,
highContrast, textDirection, needType, needTypes }`.

Non-`disabled_user` callers get 403 `ACCESSIBILITY_NOT_ALLOWED`.

## 6. Volunteer

| Method | Path | Description |
|---|---|---|
| GET/PUT | `/me/volunteer-profile` | `{introduction, helpMode, skillLevel, weeklyHours (1–168), experienceNotes, skillIds}` (200) |
| GET/PUT | `/me/volunteer-location` | `{governorate, serviceArea, latitude?, longitude?, serviceRadiusKm? (1–200, optional)}` (200) |
| GET | `/me/verification` | `Verification` status object (drives the verification wizard UI) (200) |
| POST | `/me/verification/evidence` | multipart `evidenceType` + `file` (images ≤ 10 MB); `national_id_front` \| `national_id_back` \| `face_photo` (204) |
| PUT | `/me/verification/social-links` | `{links:[{platform,url}]}`; `platform` ∈ `facebook\|linkedin\|instagram` (204) |
| GET | `/interview-slots` | the upcoming slots with their status — a **volunteer token is required** (200) |
| POST | `/me/verification/interview` | `{interviewSlotId}` — book atomically (200) |
| PUT | `/me/verification/interview` | change booking (blocked once admin approved) (200) |
| DELETE | `/me/verification/interview` | cancel booking (frees slot; blocked once approved) (200) |
| PUT | `/me/availability` | `{slots:[{startsAt, endsAt, timezone}]}` — ordered, non-overlapping, future (200) |
| GET | `/me/availability` | own availability (200) |
| GET | `/me/training` | `{moduleId, title, version, attempts:[{attemptId, score, result, completedAt}]}` (200) |
| POST | `/me/training/{moduleId}/attempts` | `{score 0–100}`; ≥ 70 = `passed` (200) |

`Verification` shape:

```json
{
  "status": "interview_scheduled",
  "currentStep": "interview",
  "reviewId": "…",
  "interviewAt": "2026-01-20T12:00:00Z",
  "canSubmit": true,
  "canBookInterview": true,
  "steps": [{ "key": "national_id_front", "titleAr": "…", "completed": true, "required": true }],
  "uploadedEvidenceTypes": ["national_id_front", "national_id_back", "face_photo"],
  "socialLinks": [{ "platform": "linkedin", "url": "https://…" }]
}
```

- `status` ∈ `unverified`, `interview_scheduled`, `approved`, `rejected`.
- `steps[].key` ∈ `email`, `phone`, `national_id_front`, `national_id_back`, `social_links`,
  `face_photo`, `interview`, `decision`.
- `currentStep` ∈ the same values plus `admin_review` (waiting for the admin), `completed`
  (approved) and `reapply` (rejected).
- `uploadedEvidenceTypes` ∈ `national_id_front`, `national_id_back`, `face_photo`.

Lifecycle: `unverified` → verify the email → verify the phone → upload the three evidence files
→ add a social link → book the interview → `interview_scheduled` → admin `approve` → `approved`,
or `rejected` (the review goes back to `draft` and the volunteer can apply again).
Rejection is silent to volunteers (no `decision_notes` in `VERIFICATION_REVIEW`; the reason lives
in `ADMIN_AUDIT`).

## 7. Organization (`association` and `company` only)

| Method | Path | Description |
|---|---|---|
| GET | `/me/organization` | Current organization profile (403 for a non-organization account) (200) |
| PUT | `/me/organization` | `{name, field, description, governorate, city, website?, employeeCountRange, contactInformation}` (200) |
| PUT | `/me/organization/logo` | multipart image (PNG/JPG ≤ 5 MB) → `{ "logoUrl": "…" }` (200) |
| PUT | `/me/organization/cover` | multipart image (PNG/JPG ≤ 5 MB) → `{ "coverUrl": "…" }` (200) |
| PUT | `/me/organization/documents/{documentType}` | `commercial_register` \| `tax_card` \| `address_proof` (PDF/JPG/PNG ≤ 10 MB); `license` optional (200) |
| POST | `/me/organization/submit` | Create/refresh admin review (200) |
| GET | `/me/organization/review-status` | `{approvalStatus, reviewId?, reviewStatus?, requestedAt?, decidedAt?, decisionNotes?, missingDocuments[], canSubmit}` (200) |

Rules:
- `documents` returns `[ {documentType, uploadedAt} ]` (no keys or URLs).
- `canSubmit` is true only when the profile is complete **and** the three required documents
  and the logo exist.
- Submit with gaps → 422 `PROFILE_INCOMPLETE` or 422 `DOCUMENTS_MISSING` (never a partial state).
- Rejected organizations stay `pending_approval` and may edit + resubmit; each submit creates a new review.
- On approval `approval_status='approved'`, `account_status='active'`, and the owner is notified.

## 8. Admin (`admin` only)

| Method | Path | Description |
|---|---|---|
| GET | `/admin/volunteer-reviews` | paged; filter `?status=` (200) |
| GET | `/admin/volunteer-reviews/{reviewId}` | detail incl. evidence short-lived URLs (200) |
| POST | `/admin/volunteer-reviews/{reviewId}/decision` | `{approve, notes?}` — `notes` is **required when `approve=false`** (204) |
| GET | `/admin/organization-reviews` | paged; filter `?status=` (200) |
| GET | `/admin/organization-reviews/{reviewId}` | detail incl. documents short-lived URLs (200) |
| POST | `/admin/organization-reviews/{reviewId}/decision` | `{approve, notes?}` — `notes` is **required when `approve=false`** (204) |
| GET | `/admin/interview-slots` | list (200) |
| POST | `/admin/interview-slots` | create `{startsAt, durationMinutes}` (201) |
| DELETE | `/admin/interview-slots/{slotId}` | free slot (available + unbooked only) (204) |
| PUT | `/admin/users/{userId}/suspension` | `{suspend, reason?}`; org suspension restores `pending_approval` when applicable (204) |
| GET | `/admin/deletion-requests` | paged queue; filter `?status=` (200) |
| POST | `/admin/deletion-requests/{id}/resolve` | `{approve, notes?}`; approve runs the anonymisation (204) |
| GET | `/admin/audit` | paged audit trail `[ {auditId, actorUserId, action, targetType, targetId, outcome, occurredAt} ]` (200) |

Reviewer evidence/document URLs are short-lived signed URLs (**10-minute** expiry).
All decisions are written to `ADMIN_AUDIT`.

## 9. Files

| Method | Path | Description |
|---|---|---|
| GET | `/files?key={key}&expires={unix}&signature={hmac}` | Validates the HMAC and streams the content (200, `application/octet-stream` or the real media type); 401 when the signature is invalid, 404 when the file is gone |

The key, the expiry and the signature are **query parameters** (the key contains slashes).
Direct media (`/media/{userId}/{file}.png`) is only for public assets (logos, covers, avatars).
Evidence and organization documents are **private** — they always require signature + expiry and
are stored outside the web root.

## 10. Real-time notifications (SignalR)

- Route: `/hubs/notifications`
- Auth: send `Authorization: Bearer <accessToken>` on negotiate. The browser transports
  (WebSocket/SSE) cannot set headers, so the negotiate call may carry the token as the
  `access_token` query parameter.
- Client method: **`notificationReceived(NOTIFICATION)`** with the payload:

```json
{
  "id": "…",
  "type": "organization_approved",
  "titleAr": "تم اعتماد مؤسستك",
  "bodyAr": "تم اعتماد مؤسستك. يمكنك الآن نشر الفرص والخدمات.",
  "createdAt": "2026-01-15T10:30:00Z",
  "payloadJson": "{\"channel\":\"in_app\",\"targetType\":\"organization_review\",\"targetId\":\"…\"}"
}
```

- `type` ∈ `organization_approved`, `organization_rejected`, `verification_approved`,
  `verification_rejected`, `interview_booked`, `interview_changed`, `interview_cancelled`,
  `account_suspended`, `account_reactivated`.
- Emitted on: organization approval/rejection, volunteer verification approval/rejection,
  interview booked/changed/cancelled, suspension and reactivation.

### Notification centre (REST)

The same rows that are pushed over SignalR are readable as history, so a client that was
offline can recover what it missed.

```
GET  /api/v1/me/notifications?page=1&pageSize=20
PUT  /api/v1/me/notifications/{notificationId}/read
```

```json
{
  "items": [
    {
      "id": "…",
      "type": "organization_approved",
      "titleAr": "تم اعتماد مؤسستك",
      "bodyAr": "تم اعتماد مؤسستك. يمكنك الآن نشر فرصك وخدماتك.",
      "createdAt": "2026-01-15T10:30:00Z",
      "isRead": false,
      "readAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

- Ordering is `createdAt` **descending**; the id breaks ties, so paging is stable.
- `PUT …/read` writes `NOTIFICATION.read_at` once. A second call answers 204 without moving the
  timestamp (idempotent), and another user's id answers 404 `NOT_FOUND` (no existence leak).
- **Limitation:** `payloadJson` (the target type/id) exists only in the live SignalR event.
  `NOTIFICATION` has no column for it, so history items do not carry it; a deep link from a
  historical row needs the deferred `payload_json` column (see `docs/DEVIATIONS.md`, entry 19).

## 11. Uploads

| Category | Limit | Error |
|---|---|---|
| Logo, cover, avatar | 5 MB | `FILE_TOO_LARGE` (413) |
| Organization documents | 10 MB | `FILE_TOO_LARGE` (413) |
| Verification evidence | 10 MB | `FILE_TOO_LARGE` (413) |
| Any request | the endpoint limit (file limit + 1 MB) | `REQUEST_TOO_LARGE` (413) |

The extension, the content type and the magic bytes are all validated; a rejected file answers
400 `FILE_TYPE_NOT_ALLOWED`.

## 12. Non-functional guarantees

- **No PII in logs**: Serilog never writes the request URLs (they carry the file signatures and
  the SignalR token) and no email, phone or token reaches the log file.
- **Envelope consistency**: even 5xx responses pass through the exception middleware and use the
  standard envelope with `INTERNAL_ERROR`.
- **Idempotent lookups**: GET endpoints are side-effect free; PUT upserts replace-entire-set semantics.
- **Localization**: `message` is always Arabic (Egyptian); `code` is stable ASCII.
- **OpenAPI**: served at `/swagger/v1/swagger.json` **in development only**; the exported copy is
  committed to `docs/openapi.json` with a CI drift check.
- **Rate limiting**: the authentication, OTP and upload endpoints are limited per identity; the
  answer carries `Retry-After`.
