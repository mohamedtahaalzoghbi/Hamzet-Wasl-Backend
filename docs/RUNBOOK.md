# Hamzet Wasl — Runbook

Everything needed to build, run, verify, and ship Phase 1 on a developer machine or CI.

## 1. Prerequisites

| Component | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --info` must list `net10.0` |
| SQL Server LocalDB | MSSQLLocalDB | `(localdb)\MSSQLLocalDB`; part of VS / standalone MSI |
| sqlcmd / SSMS | optional | only to inspect data manually |

Verify LocalDB:

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

## 2. Configuration

`src/HamzetWasl.Api/appsettings.json` holds the base settings, overridden by
`appsettings.Development.json` (development only, committed with explicit DEV-ONLY keys):

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server / LocalDB connection |
| `Jwt:Issuer`, `Jwt:Audience` | Token issuer/audience |
| `Jwt:SigningKey` | ⚠ minimum 32 chars (HTTPS HSA256) |
| `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays` | Token lifetimes |
| `Storage:RootPath`, `Storage:PublicBaseUrl`, `Storage:SigningKey` | Private/local file storage + HMAC key |
| `Otp:HashKey` | Pepper for hashing OTP codes |
| `Seed:AdminEmail`, `Seed:AdminPhone`, `Seed:AdminPassword` | First admin account |
| `SkipDatabaseInitialization` | `true` skips automatic schema/seeding (used by tests) |

Never commit production keys; override with environment variables, e.g.
`Jwt__SigningKey`, `ConnectionStrings__DefaultConnection`.

## 3. Run the API

```powershell
dotnet run --project src/HamzetWasl.Api
```

First start performs (unless `SkipDatabaseInitialization=true`):

1. Applies `Hamzet_Wasl_Schema_v2.sql` (all 60 domain tables) if the database is empty.
2. `IdentitySchemaInitializer` creates the `AspNet*` Identity tables.
3. `DatabaseSeeder` seeds roles, lookups, training module, feature registry, and the admin user.

Then:

- Swagger UI: `http://localhost:5153/swagger`
- Health: `GET http://localhost:5153/health` → `Healthy`
- SignalR hub: `/hubs/notifications` (Bearer negotiation)

> ⚠ Windows Smart App Control: on some machines SAC blocks freshly built unsigned DLLs
> (`0x800711C7`, Event 3033/3077). Either turn Smart App Control **off** (Settings →
> Windows Security) or run tests/CI elsewhere. This affects every fresh build, not only
> this project.

## 4. Default admin (Development)

```
email:    admin@hamzetwasl.local
phone:    01000000000
password: Admin@12345
```

## 5. Tests

```powershell
dotnet test tests/HamzetWasl.UnitTests/HamzetWasl.UnitTests.csproj        # fast, no DB
dotnet test tests/HamzetWasl.IntegrationTests/HamzetWasl.IntegrationTests.csproj
```

Integration tests:

- create a unique `HamzetWasl_IT_<guid>` LocalDB database,
- apply `Hamzet_Wasl_Schema_v2.sql` exactly as shipped (batch splitter on `GO`),
- boot the real API through `WebApplicationFactory` with `SkipDatabaseInitialization=true`,
- run end-to-end Phase-1 flows, then drop the database.

They need LocalDB running and SAC off (see warning above).

## 6. OpenAPI export + drift check

```powershell
# Start the app once, then:
Invoke-WebRequest -Uri http://localhost:5153/swagger/v1/swagger.json -OutFile docs/openapi.json
dotnet run --project tools/OpenApiCheck   # compares docs/openapi.json with the live document
```

CI (GitHub Actions, `ci.yml`):

1. `dotnet build -warnaserror` on `src/**` (0 warnings required).
2. Run unit tests.
3. Start the API with a throwaway LocalDB and export `swagger.json`.
4. `OpenApiCheck` fails the build when routes/schemas drift from the committed
   `docs/openapi.json` — contract changes must update the file in the same PR.

## 7. Manual verification checklist (Phase 1)

Happy paths, in order, via Swagger (`Authorize` with the `accessToken`):

1. `POST /api/v1/auth/register` (role `disabled_user`) → 201 + tokens.
2. `PUT /api/v1/me/profile` → 200; `GET` shows the change.
3. `PUT /api/v1/me/accessibility` → 200; `GET` returns `effectivePreset`; `DELETE` then `GET` → 404.
4. Register `volunteer`, `GET /api/v1/me/verification` → wizard steps.
5. As admin create `POST /api/v1/admin/interview-slots`; volunteer books it; admin decides.
6. Register `company`; `PUT /api/v1/me/organization`; upload logo + 3 documents; `submit`;
   admin rejects → edit → resubmit → approve; `review-status` shows `approved`.
7. `GET /api/v1/admin/audit` shows every decision with actor and target.
8. Second user must get 403/404 on anything belonging to the first (ownership rules).

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `ConnectionStrings:DefaultConnection is required` | config file not loaded — run via `dotnet run`, or set the env var, and start from the API folder |
| `Jwt:SigningKey must be at least 32 characters` | missing `appsettings.Development.json` values; set `Jwt__SigningKey` |
| `An Application Control policy has blocked this file` (0x800711C7) | Smart App Control; disable it or run on another machine |
| SQL "database already exists" | test DBs are named `HamzetWasl_IT_*`; drop leftovers with `sqllocaldb` |
| `/media` 404 for evidence URLs | private files need signed `/api/v1/files/...` URLs; only public assets use `/media` |
| OTP never verifies in dev | seeded sender writes codes to logs in Development; check console/Serilog output |

## 9. Definition of done (Phase 1)

- [x] `dotnet build` → 0 errors, 0 warnings across the solution.
- [x] Business rules from MASTER_PROMPT §1.9 covered by unit tests.
- [x] Integration tests cover: onboarding, profile/accessibility flows, volunteer
      verification, organization onboarding, admin queues/audit, ownership rules.
- [x] `docs/openapi.json` committed and CI-validated.
- [x] `docs/API_CONTRACT.md`, `docs/ERD_TO_API_MAP.md`, `docs/OPEN_QUESTIONS.md`, this runbook.
- [x] No PII in logs; no cascade deletes; schema file untouched.
