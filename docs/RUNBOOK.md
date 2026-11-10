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

### Repository and CI

| Item | Value |
|---|---|
| Remote | `https://github.com/mohamedtahaalzoghbi/hamzet-wasl-backend` (private) |
| Branch | `main` |
| Pipeline | `.github/workflows/ci.yml` — build (warnings as errors), unit + integration tests on a SQL Server 2022 container, OpenAPI drift check, secret scan |
| Last green run | 121 unit tests + 30 integration tests, 0 warnings, `docs/openapi.json` in sync |

Any push to `main` (and every pull request) runs the pipeline; the branch must stay green.

## 2. Configuration

`src/HamzetWasl.Api/appsettings.json` holds the base settings, overridden by
`appsettings.Development.json` (development only, committed with explicit DEV-ONLY keys):

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server / LocalDB connection |
| `Jwt:Issuer`, `Jwt:Audience` | Token issuer/audience |
| `Jwt:SigningKey` | ⚠ minimum 32 chars (HS256) — **required**, the app refuses to start without it |
| `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays` | Token lifetimes |
| `Storage:RootPath`, `Storage:PublicBaseUrl`, `Storage:SigningKey` | Private/local file storage + HMAC key (⚠ minimum 32 chars, **required**) |
| `Otp:HashKey` | Pepper for hashing OTP codes (⚠ minimum 16 chars, **required**) |
| `Seed:AdminEmail`, `Seed:AdminPhone`, `Seed:AdminPassword` | First admin account |
| `RateLimiting:*` | Limits of the fixed window limiter (auth 20/min, OTP 6/10 min, uploads 30/h) |
| `Swagger:Enabled` | Serves the OpenAPI document; default `false` outside development |
| `Serilog:MinimumLevel:Override` | Silences the request and SQL loggers (the request URL carries signatures and tokens) |
| `SkipDatabaseInitialization` | `true` skips automatic schema/seeding (used by tests) |

Never commit production keys; override with environment variables, e.g.
`Jwt__SigningKey`, `Storage__SigningKey`, `Otp__HashKey`,
`ConnectionStrings__DefaultConnection`. There are no hard-coded fallback keys in the code:
a missing key stops the application with a clear error instead of silently using a default.

## 3. Run the API

```powershell
dotnet run --project src/HamzetWasl.Api
```

First start performs (unless `SkipDatabaseInitialization=true`):

1. Creates the database if it does not exist yet.
2. Applies `Hamzet_Wasl_Schema_v2.sql` (all 60 domain tables) if the domain tables are missing.
3. `IdentitySchemaInitializer` creates the `AspNet*` Identity tables.
4. `DatabaseSeeder` seeds roles, lookups, training module, feature registry, and the admin user.

The schema file is embedded in `HamzetWasl.Infrastructure`, so no manual `sqlcmd` step is needed.
EF Core migrations are **not** used: the SQL script is the single source of truth.

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

Unit tests (121) cover the §1.9 business rules, the validators, the phone normaliser, the file
rules, the token purposes, the UTC serialisation, the accessibility preset and the **schema parity
test** (EF model vs `Hamzet_Wasl_Schema_v2.sql`).

Integration tests (30) run against a real SQL Server:

- create a unique `HamzetWasl_IT_<guid>` database,
- apply `Hamzet_Wasl_Schema_v2.sql` exactly as shipped (batch splitter on `GO`),
- boot the real API through `WebApplicationFactory` with `SkipDatabaseInitialization=true`,
- run the Phase-1 end-to-end flows, the applied-schema assertions and the security regressions
  (token purposes, lockout, phone ownership, enumeration, rate limiting, upload limits), then drop
  the database.

Locally they use LocalDB. On CI (or any other server) point them at it with environment
variables:

| Variable | Example |
|---|---|
| `HW_TEST_SQL_SERVER` | `localhost,1433` |
| `HW_TEST_SQL_USER` | `sa` |
| `HW_TEST_SQL_PASSWORD` | `Password123!` |

When these are absent the tests fall back to `(localdb)\MSSQLLocalDB` with a trusted connection.
They need LocalDB running and SAC off (see warning above).

## 6. OpenAPI export + drift check

```powershell
# Start the app, then:
Invoke-WebRequest -Uri http://localhost:5153/swagger/v1/swagger.json -OutFile docs/openapi.json
```

The committed file must stay byte-comparable with the live document. CI (GitHub Actions,
`.github/workflows/ci.yml`) does it automatically:

1. `dotnet build -p:TreatWarningsAsErrors=true` (0 warnings required).
2. Unit tests, then integration tests against a SQL Server 2022 service container.
3. The API is started against a throwaway database and `swagger.json` is exported.
4. `jq -S` compares the export with `docs/openapi.json`; a difference fails the build —
   contract changes must update the file in the same commit.
   The document is only served in development, so the CI job starts the API with
   `ASPNETCORE_ENVIRONMENT=Development` (the API creates the database and applies the schema).

## 6.1 Continuous integration jobs

| Job | What it guarantees |
|---|---|
| `build-and-test` | 0 warnings, unit + integration tests green |
| `openapi-drift` | `docs/openapi.json` matches the running API |
| `no-secrets` | no credential literal is committed (production config must be empty, development config only `DEV-ONLY` placeholders) |

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
| OTP never verifies in dev | the development sender only logs that a message was sent (no code in the logs). The integration tests capture the code through `CapturingMessageSender`; for manual testing call `/auth/email/send-code` and read the code from the configured SMTP/SMS gateway |

## 9. Definition of done (Phase 1)

- [x] `dotnet build` → 0 errors, 0 warnings across the solution (also enforced in CI).
- [x] Business rules from MASTER_PROMPT §1.9 covered by unit tests.
- [x] Integration tests cover: onboarding, profile/accessibility flows, volunteer
      verification, organization onboarding, admin queues/audit, ownership rules, the
      applied-schema assertions (tables, CHECK constraints, FKs, unique constraints, indexes)
      and the P0 security regressions.
- [x] `docs/openapi.json` committed, accurate (status codes, error responses, security,
      enums) and CI-validated (drift check).
- [x] `docs/API_CONTRACT.md`, `docs/ERD_TO_API_MAP.md`, `docs/OPEN_QUESTIONS.md`,
      `docs/DEVIATIONS.md`, this runbook.
- [x] No PII in logs; no cascade deletes; schema file untouched.
