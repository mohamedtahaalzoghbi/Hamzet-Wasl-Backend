<div align="center">
  <img src="docs/logo.svg" alt="Hamzet Wasl logo" width="120" height="120">
</div>

# Ù‡Ù…Ø²Ø© ÙˆØµÙ„ â€” Hamzet Wasl Backend

<div align="center">

**Ù…Ù†ØµØ© ØªØ±Ø¨Ø· Ø°ÙˆÙŠ Ø§Ù„Ù‡Ù…Ù… Ø¨Ø§Ù„Ù…ØªØ·ÙˆØ¹ÙŠÙ† ÙˆØ§Ù„Ø¬Ù…Ø¹ÙŠØ§Øª ÙˆØ§Ù„Ø´Ø±ÙƒØ§Øª**

`REST API` Â· `.NET 10` Â· `SQL Server` Â· `Clean Architecture` Â· `OpenAPI 3.0.4`

[![Tests](https://img.shields.io/badge/tests-172%20passing-12A4A4?style=flat-square)](https://github.com/)
[![Build](https://img.shields.io/badge/build-0%20warnings-0F4C81?style=flat-square)](https://github.com/)
[![OpenAPI](https://img.shields.io/badge/OpenAPI-3.0.4-12A4A4?style=flat-square)](docs/openapi.json)

</div>

---

<div dir="rtl">

## Ù†Ø¸Ø±Ø© Ø¹Ø§Ù…Ø©

**Ù…Ù†ØµØ© ØªÙ…ÙƒÙ‘Ù† Ø°ÙˆÙŠ Ø§Ù„Ù‡Ù…Ù… Ù…Ù† Ø¥ÙŠØ¬Ø§Ø¯ Ø§Ù„Ø¯Ø¹Ù… ÙˆØ§Ù„Ø¹Ù…Ù„ØŒ ÙˆØªØ±Ø¨Ø·Ù‡Ù… Ø¨Ø§Ù„Ù…ØªØ·ÙˆØ¹ÙŠÙ† ÙˆØ§Ù„Ø¬Ù…Ø¹ÙŠØ§Øª ÙˆØ§Ù„Ø´Ø±ÙƒØ§Øª Ø§Ù„ØªÙŠ
ØªÙ‚Ø¯Ù‘Ù… ÙØ±ØµØ§Ù‹ ÙˆØ®Ø¯Ù…Ø§Øª. Ù‡Ø°Ø§ Ø§Ù„Ù…Ø³ØªÙˆØ¯Ø¹ ÙŠØ­ØªÙˆÙŠ **ÙˆØ§Ø¬Ù‡Ø© Ø§Ù„Ù€ Backend** (Ø§Ù„Ù…Ø±Ø­Ù„Ø© Ø§Ù„Ø£ÙˆÙ„Ù‰
Ù…ÙƒØªÙ…Ù„Ø©): Ù…ØµØ§Ø¯Ù‚Ø© JWTØŒ Ù…Ù„ÙØ§Øª Ø´Ø®ØµÙŠØ©ØŒ Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª Ø¥ØªØ§Ø­Ø©ØŒ ØªØ­Ù‚Ù‘Ù‚ Ø§Ù„Ù…ØªØ·ÙˆØ¹ÙŠÙ†ØŒ Ø¯ÙˆØ±Ø© Ø­ÙŠØ§Ø© Ø§Ù„Ù…Ù†Ø¸Ù…Ø§ØªØŒ
Ù„ÙˆØ­Ø© Ø¥Ø¯Ø§Ø±Ø©ØŒ Ø¥Ø´Ø¹Ø§Ø±Ø§Øª Ù„Ø­Ø¸ÙŠØ© Ø¹Ø¨Ø± SignalRØŒ Ùˆ62 Ø¬Ø¯ÙˆÙ„Ø§Ù‹ ÙÙŠ Ù…Ø®Ø·Ø· Ù…Ø±Ø¬Ø¹ÙŠ ÙˆØ§Ø­Ø¯.

</div>

---

## Ø§Ù„ØªÙ‚Ù†ÙŠØ§Øª Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…Ø©

| Ø§Ù„Ø·Ø¨Ù‚Ø© | Ø§Ù„ØªÙ‚Ù†ÙŠØ© | Ù…Ù„Ø§Ø­Ø¸Ø© |
|---|---|---|
| Runtime | **.NET 10** (`net10.0`) | LTSØŒ Ù…Ø¹ `Nullable` Ùˆ`ImplicitUsings` |
| Web API | **ASP.NET Core** | Controllers + Middleware + Filters |
| Architecture | **Clean Architecture** | `Api â†’ Application â†’ Domain` Ùˆ `Infrastructure â†’ Application â†’ Domain` |
| Database | **SQL Server** | 62 Ø¬Ø¯ÙˆÙ„Ø§Ù‹ ÙÙŠ `Hamzet_Wasl_Schema_v2.sql` (Ø¨Ø¯ÙˆÙ† EF migrations) |
| ORM | Entity Framework Core 10 | Ø§Ù„Ù…Ø®Ø·Ø· SQL Ù‡Ùˆ Ø§Ù„Ù…ØµØ¯Ø± Ø§Ù„ÙˆØ­ÙŠØ¯ Ù„Ù„Ø­Ù‚ÙŠÙ‚Ø© |
| Auth | ASP.NET Core Identity + JWT Bearer | Access 15 Ø¯Ù‚ÙŠÙ‚Ø© Â· Refresh 30 ÙŠÙˆÙ… Ù…Ø¹ ØªØ¯ÙˆÙŠØ± |
| Validation | FluentValidation 12 | Ø±Ø³Ø§Ø¦Ù„ Ø¹Ø±Ø¨ÙŠØ© + Ø£ÙƒÙˆØ§Ø¯ Ø£Ø®Ø·Ø§Ø¡ Ø«Ø§Ø¨ØªØ© |
| Docs | Swashbuckle (OpenAPI 3.0.4) | Ù…ÙØµØ¯ÙŽÙ‘Ø± Ø¢Ù„ÙŠØ§Ù‹ Ø¥Ù„Ù‰ `docs/openapi.json` |
| Realtime | SignalR | Ø¥Ø´Ø¹Ø§Ø±Ø§Øª Ù„Ø­Ø¸ÙŠØ© Ø¹Ù„Ù‰ `/hubs/notifications` |
| Logging | Serilog | Console + FileØŒ Ø¨Ø¯ÙˆÙ† Ø¨ÙŠØ§Ù†Ø§Øª Ø´Ø®ØµÙŠØ© |
| Tests | **xUnit** + FluentAssertions + `WebApplicationFactory` | **131** Ø§Ø®ØªØ¨Ø§Ø± ÙˆØ­Ø¯Ø© + **41** Ø§Ø®ØªØ¨Ø§Ø± ØªÙƒØ§Ù…Ù„ |
| CI | GitHub Actions | Ø¨Ù†Ø§Ø¡ + Ø§Ø®ØªØ¨Ø§Ø±Ø§Øª + ÙØ­Øµ ØªØ·Ø§Ø¨Ù‚ OpenAPI + ÙØ­Øµ Ø£Ø³Ø±Ø§Ø± |

---

## âš™ï¸ Ø§Ù„ØªØ´ØºÙŠÙ„ (Setup) Ù„Ù…Ø·ÙˆÙ‘Ø± Ø¬Ø¯ÙŠØ¯

### Ø§Ù„Ù…ØªØ·Ù„Ø¨Ø§Øª

| Ø§Ù„Ø£Ø¯Ø§Ø© | Ø§Ù„Ø¥ØµØ¯Ø§Ø± |
|---|---|
| .NET SDK | `10.0.x` |
| SQL Server | LocalDB (Ù„Ù„ØªØ·ÙˆÙŠØ±) Ø£Ùˆ SQL Server 2019+ |
| sqlcmd / SSMS | Ø§Ø®ØªÙŠØ§Ø±ÙŠ (Ù„ØªØ´ØºÙŠÙ„ Ø³ÙƒØ±Ø¨ØªØ§Øª Ø§Ù„Ù…Ø®Ø·Ø·) |

### 1) Ø§Ø³ØªÙ†Ø³Ø§Ø® Ø§Ù„Ù…Ø´Ø±ÙˆØ¹

```bash
git clone https://github.com/mohamedtahaalzoghbi/Hamzet-Wasl-Backend.git
cd Hamzet-Wasl-Backend
```

### 2) ØªØ´ØºÙŠÙ„ Ø³ÙƒØ±Ø¨Øª Ù‚Ø§Ø¹Ø¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª

Ø§Ù„Ù…Ø®Ø·Ø· Ø§Ù„Ù…Ø±Ø¬Ø¹ÙŠ Ù‡Ùˆ `Hamzet_Wasl_Schema_v2.sql` (62 Ø¬Ø¯ÙˆÙ„Ø§Ù‹ + Ù‚ÙŠÙˆØ¯ CHECK Ùˆ Foreign keys).
ÙŠÙ…ÙƒÙ† ØªØ´ØºÙŠÙ„Ù‡ Ø¨Ø³Ø·Ø± ÙˆØ§Ø­Ø¯:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Schema_v2.sql
```

> **Ø¨Ø¯ÙŠÙ„ Ù…Ø®ØªØµØ±:** Ù„Ùˆ ØªØ±ÙƒØª Ø§Ù„Ù€ API ÙŠÙ‚Ù„Ø¹ Ø¹Ù„Ù‰ Ù‚Ø§Ø¹Ø¯Ø© Ø¨ÙŠØ§Ù†Ø§Øª ÙØ§Ø±ØºØ©ØŒ Ø³ÙŠØ·Ø¨Ù‘Ù‚ Ø§Ù„Ø³ÙƒØ±Ø¨Øª **ØªÙ„Ù‚Ø§Ø¦ÙŠØ§Ù‹**
> (Ø§Ù„Ø³ÙƒØ±Ø¨Øª Ù…ÙØ¶Ù…ÙŽÙ‘Ù† Ø¯Ø§Ø®Ù„ Ø§Ù„Ù…Ø´Ø±ÙˆØ¹ ÙˆÙ„Ø§ ØªÙˆØ¬Ø¯ EF Core migrations). Ø§Ù„Ø®Ø·ÙˆØ§Øª 2 Ùˆ3 Ù…Ø·Ù„ÙˆØ¨Ø© ÙÙ‚Ø·
> Ø¥Ù† ÙƒØ§Ù†Øª Ù„Ø¯ÙŠÙƒ Ù‚Ø§Ø¹Ø¯Ø© Ø¨ÙŠØ§Ù†Ø§Øª Ù‚Ø¯ÙŠÙ…Ø©.

### 3) ØªØ´ØºÙŠÙ„ Ø³ÙƒØ±Ø¨ØªØ§Øª Ø§Ù„ØªØ±Ù‚ÙŠØ© (Migrations) Ù„Ù„Ù‚Ø§Ø¹Ø¯Ø© Ø§Ù„Ù‚Ø¯ÙŠÙ…Ø©

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_1_review_created_at.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_2_geo_tables.sql
```

ÙƒÙ„Ø§Ù‡Ù…Ø§ **idempotent** (ÙŠØ¹Ù…Ù„ Ø¨Ø¯ÙˆÙ† Ø£Ø®Ø·Ø§Ø¡ Ø¥Ù† Ø·ÙØ¨Ù‘Ù‚ Ù…Ø±ØªÙŠÙ†) ÙˆÙŠØ­ØªÙˆÙŠ Ø¹Ù„Ù‰ rollback Ù…ÙˆØ«Ù‘Ù‚.

### 4) Ø¶Ø¨Ø· Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª (Ù…Ù‡Ù…)

Ø§Ù„Ù…Ø³ØªÙˆØ¯Ø¹ **Ù„Ø§ ÙŠØ­ØªÙˆÙŠ Ø£ÙŠ Ø£Ø³Ø±Ø§Ø±**. ÙƒÙ„ Ù‚ÙŠÙ…Ø© Ø­Ø³Ø§Ø³Ø© ØªØ£ØªÙŠ Ù…Ù† Ù…ØªØºÙŠØ±Ø§Øª Ø§Ù„Ø¨ÙŠØ¦Ø© Ø£Ùˆ Ù…Ù† Ù…Ù„Ù Ù…Ø­Ù„ÙŠ
Ù„Ø§ ÙŠÙØ±ÙØ¹ Ø¥Ù„Ù‰ Git:

```powershell
# Ø§Ù†Ø³Ø® Ù…Ù„Ù Ø§Ù„Ù‚Ø§Ù„Ø¨ Ø¥Ù„Ù‰ Ù…Ù„Ù Ù…Ø­Ù„ÙŠ (Ø§Ù„Ù…Ø¬Ù„Ø¯ Ù…ÙØªØ¬Ø§Ù‡ÙŽÙ„ ÙÙŠ .gitignore)
copy src\HamzetWasl.Api\appsettings.Development.example.json src\HamzetWasl.Api\appsettings.Development.json
```

Ø«Ù… Ø¹Ø¯Ù‘Ù„ Ø§Ù„Ù‚ÙŠÙ… Ø¯Ø§Ø®Ù„ `appsettings.Development.json`:

| Ø§Ù„Ù…ÙØªØ§Ø­ | Ø§Ù„ÙˆØµÙ |
|---|---|
| `Jwt:SigningKey` | Ù…ÙØªØ§Ø­ ØªÙˆÙ‚ÙŠØ¹ Ø§Ù„ØªÙˆÙƒÙ† (32 Ø­Ø±ÙØ§Ù‹ Ø¹Ù„Ù‰ Ø§Ù„Ø£Ù‚Ù„) |
| `Storage:SigningKey` | Ù…ÙØªØ§Ø­ ØªÙˆÙ‚ÙŠØ¹ Ø±ÙˆØ§Ø¨Ø· Ø§Ù„Ù…Ù„ÙØ§Øª (32 Ø­Ø±ÙØ§Ù‹ Ø¹Ù„Ù‰ Ø§Ù„Ø£Ù‚Ù„) |
| `Otp:HashKey` | Ù…ÙØªØ§Ø­ hash Ù„Ø£ÙƒÙˆØ§Ø¯ OTP (16 Ø­Ø±ÙØ§Ù‹ Ø¹Ù„Ù‰ Ø§Ù„Ø£Ù‚Ù„) |
| `Seed:AdminPassword` | ÙƒÙ„Ù…Ø© Ù…Ø±ÙˆØ± Ø£ÙˆÙ„ Ø­Ø³Ø§Ø¨ Ø£Ø¯Ù…Ù† |

> Ù†Ø³Ø®Ø© Ø¬Ø§Ù‡Ø²Ø© Ù„Ù„Ø§Ø³ØªØ®Ø¯Ø§Ù… Ø§Ù„Ù…Ø­Ù„ÙŠ (ØºÙŠÙ‘Ø±Ù‡Ø§ Ù‚Ø¨Ù„ Ø£ÙŠ Ù†Ø´Ø± Ù…Ø´ØªØ±Ùƒ):
> `Jwt:SigningKey = DEV-ONLY-local-jwt-key-change-me-0123456789` Â·
> `Storage:SigningKey = DEV-ONLY-local-storage-key-change-me-0123456789` Â·
> `Otp:HashKey = DEV-ONLY-local-otp-key-change-me-0123456789` Â·
> `Seed:AdminPassword = Admin@12345`

Ø¨Ø¯ÙŠÙ„ Ù„Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª: Ù…ØªØºÙŠØ±Ø§Øª Ø§Ù„Ø¨ÙŠØ¦Ø© (`Jwt__SigningKey`ØŒ `Storage__SigningKey`ØŒ `Otp__HashKey`) Ø£Ùˆ
`dotnet user-secrets`. Ù„Ù„Ø¥Ù†ØªØ§Ø¬ Ø§Ø³ØªØ®Ø¯Ù… `src/HamzetWasl.Api/appsettings.Production.example.json`
ÙƒÙ‚Ø§Ù„Ø¨ + Ø£Ø³Ø±Ø§Ø± Ù…Ù† Ù…Ø®Ø²Ù† Ø£Ø³Ø±Ø§Ø±.

### 5) Ø§Ù„ØªØ´ØºÙŠÙ„

```bash
dotnet restore HamzetWasl.slnx
dotnet run --project src/HamzetWasl.Api
```

| Ø§Ù„Ø±Ø§Ø¨Ø· | Ø§Ù„ØºØ±Ø¶ |
|---|---|
| `http://localhost:5153/swagger` | **Swagger UI** (Ø¨ÙŠØ¦Ø© Ø§Ù„ØªØ·ÙˆÙŠØ± ÙÙ‚Ø·) |
| `http://localhost:5153/swagger/v1/swagger.json` | Ù…Ø³ØªÙ†Ø¯ OpenAPI |
| `http://localhost:5153/health` | ÙØ­Øµ Ø§Ù„ØµØ­Ø© |
| `http://localhost:5153/hubs/notifications` | SignalR (ÙŠØªØ·Ù„Ø¨ Bearer token) |

Ø¹Ù†Ø¯ Ø£ÙˆÙ„ ØªØ´ØºÙŠÙ„: Ø¥Ù†Ø´Ø§Ø¡ Ø§Ù„Ù‚Ø§Ø¹Ø¯Ø© â† ØªØ·Ø¨ÙŠÙ‚ Ø§Ù„Ù…Ø®Ø·Ø· â† Ø¥Ù†Ø´Ø§Ø¡ Ø¬Ø¯Ø§ÙˆÙ„ Identity â† Ø¨Ø°Ø± Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ù…Ø±Ø¬Ø¹ÙŠØ©
(Ø§Ù„Ø£Ø¯ÙˆØ§Ø±ØŒ Ø§Ù„Ù…Ù‡Ø§Ø±Ø§ØªØŒ Ø§Ù„Ù…Ø­Ø§ÙØ¸Ø§ØªØŒ Ø§Ù„Ù…Ù†Ø§Ø·Ù‚) ÙˆØ­Ø³Ø§Ø¨ Ø§Ù„Ø£Ø¯Ù…Ù†.

### 6) (Ø§Ø®ØªÙŠØ§Ø±ÙŠ) Ø­Ø³Ø§Ø¨Ø§Øª Ø§Ù„Ø¹Ø±Ø¶

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\seed-demo-accounts.ps1
```

ÙŠÙ†Ø´Ø¦ Ø­Ø³Ø§Ø¨Ø§Øª Ù…ØªØ·ÙˆØ¹ ÙˆØ¬Ù…Ø¹ÙŠØ© ÙˆØ´Ø±ÙƒØ© ÙˆØ°ÙˆÙŠ Ù‡Ù…Ù… Ø¨ÙƒÙ„Ù…Ø© Ù…Ø±ÙˆØ± `Demo@12345` ÙˆØ¨Ù…Ù„ÙØ§Øª Ø¬Ø§Ù‡Ø²Ø© Ù„Ù„Ø¹Ø±Ø¶.

---

## ðŸ§ª ØªØ´ØºÙŠÙ„ Ø§Ù„Ø§Ø®ØªØ¨Ø§Ø±Ø§Øª

```bash
# ÙƒÙ„ Ø§Ù„Ø§Ø®ØªØ¨Ø§Ø±Ø§Øª (172 Ø§Ø®ØªØ¨Ø§Ø±Ø§Ù‹)
dotnet test HamzetWasl.slnx -c Release

# Ø£Ùˆ ÙƒÙ„ Ù…Ø¬Ù…ÙˆØ¹Ø© Ø¹Ù„Ù‰ Ø­Ø¯Ø©
dotnet test tests/HamzetWasl.UnitTests/HamzetWasl.UnitTests.csproj -c Release
dotnet test tests/HamzetWasl.IntegrationTests/HamzetWasl.IntegrationTests.csproj -c Release
```

| Ø§Ù„Ù…Ø¬Ù…ÙˆØ¹Ø© | Ø§Ù„Ø¹Ø¯Ø¯ | Ù…Ø§Ø°Ø§ ØªØºØ·ÙŠ |
|---|---|---|
| Unit | **131** | Ù‚ÙˆØ§Ø¹Ø¯ Ø§Ù„Ø¹Ù…Ù„ØŒ Ø§Ù„Ù€ validatorsØŒ ØªØ·Ø¨ÙŠØ¹ Ø§Ù„Ù‡Ø§ØªÙØŒ Ù‚ÙˆØ§Ø¹Ø¯ Ø§Ù„Ù…Ù„ÙØ§ØªØŒ Ø§Ù„ØªÙˆÙƒÙ†Ø§ØªØŒ ØªÙˆÙ‚ÙŠØªØ§Øª UTCØŒ Ø­Ø³Ø§Ø¨ Ø§Ù„Ù€ presetØŒ Ø§Ù„ÙƒØªØ§Ù„ÙˆØ¬ Ø§Ù„Ø¬ØºØ±Ø§ÙÙŠØŒ **ØªØ·Ø§Ø¨Ù‚ Ø§Ù„Ù…Ø®Ø·Ø·** |
| Integration | **41** | ØªØ¯ÙÙ‚Ø§Øª Ø­Ù‚ÙŠÙ‚ÙŠØ© Ø¹Ø¨Ø± HTTP Ø¹Ù„Ù‰ SQL Server Ø­Ù‚ÙŠÙ‚ÙŠ (ØªØ³Ø¬ÙŠÙ„ØŒ Ø±ÙØ¹ØŒ Ù…Ø±Ø§Ø¬Ø¹Ø©ØŒ Ø¥Ø´Ø¹Ø§Ø±Ø§ØªØŒ ØµÙ„Ø§Ø­ÙŠØ§ØªØŒ Ø­Ø¯ Ù…Ø¹Ø¯Ù„ØŒ Ø·Ù„Ø¨Ø§Øª Ø­Ø°Ù) |

Ø§Ø®ØªØ¨Ø§Ø±Ø§Øª Ø§Ù„ØªÙƒØ§Ù…Ù„ ØªÙ†Ø´Ø¦ Ù‚Ø§Ø¹Ø¯Ø© `HamzetWasl_IT_<guid>` Ù…Ø¤Ù‚ØªØ© ÙˆØªØ·Ø¨Ù‘Ù‚ Ø§Ù„Ù…Ø®Ø·Ø· Ø§Ù„Ù…Ø±Ø¬Ø¹ÙŠ ÙƒÙ…Ø§ Ù‡Ùˆ Ø«Ù… ØªØ­Ø°ÙÙ‡Ø§.
Ø¹Ù„Ù‰ Ø¬Ù‡Ø§Ø² Ø¨Ø¯ÙˆÙ† LocalDB:

```bash
export HW_TEST_SQL_SERVER=localhost,1433
export HW_TEST_SQL_USER=sa
export HW_TEST_SQL_PASSWORD="<your-password>"
```

---

## ðŸ‘¥ Ø£Ø¯ÙˆØ§Ø± Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…ÙŠÙ†

| Ø§Ù„Ø¯ÙˆØ± | ÙƒÙŠÙ ÙŠÙÙ†Ø´Ø£ | Ø£Ø¨Ø±Ø² Ø§Ù„Ù…Ø³Ø§Ø±Ø§Øª |
|---|---|---|
| `disabled_user` | ØªØ³Ø¬ÙŠÙ„ Ø¹Ø§Ù… | `/me/profile`, `/me/accessibility`, Ø§Ù„Ø¥Ø´Ø¹Ø§Ø±Ø§ØªØŒ Ø·Ù„Ø¨ Ø§Ù„Ø­Ø°Ù |
| `volunteer` | ØªØ³Ø¬ÙŠÙ„ Ø¹Ø§Ù… | `/me/volunteer-profile`, `/me/volunteer-location`, `/me/verification`, `/me/availability`, `/me/training` |
| `association` | ØªØ³Ø¬ÙŠÙ„ Ø¨Ø§Ø³Ù… Ù…Ù†Ø¸Ù…Ø© | `/me/organization` (Ù…Ù„ÙØŒ Ø´Ø¹Ø§Ø±ØŒ ØºÙ„Ø§ÙØŒ Ù…Ø³ØªÙ†Ø¯Ø§ØªØŒ ØªÙ‚Ø¯ÙŠÙ…ØŒ Ù…Ø±Ø§Ø¬Ø¹Ø©) |
| `company` | ØªØ³Ø¬ÙŠÙ„ Ø¨Ø§Ø³Ù… Ø´Ø±ÙƒØ© | Ù†ÙØ³ ØµÙ„Ø§Ø­ÙŠØ§Øª `association` |
| `admin` | Ø¨Ø°Ø± ØªÙ„Ù‚Ø§Ø¦ÙŠ ÙÙ‚Ø· (ØºÙŠØ± Ù…Ø³Ø¬ÙŽÙ‘Ù„) | `/admin/volunteer-reviews`, `/admin/organization-reviews`, `/admin/interview-slots`, `/admin/deletion-requests`, `/admin/audit` |

---

## ðŸ“š Ø§Ù„ØªÙˆØ«ÙŠÙ‚

| Ø§Ù„Ù…Ù„Ù | Ø§Ù„Ù…Ø­ØªÙˆÙ‰ |
|---|---|
| [`docs/API_CONTRACT.md`](docs/API_CONTRACT.md) | Ø§Ù„Ø¹Ù‚Ø¯ Ø§Ù„ÙƒØ§Ù…Ù„ Ù„ÙƒÙ„ Ù†Ù‚Ø·Ø© Ù†Ù‡Ø§ÙŠØ© |
| [`docs/openapi.json`](docs/openapi.json) | Ù…Ø³ØªÙ†Ø¯ OpenAPI 3.0.4 Ù…ÙØµØ¯ÙŽÙ‘Ø± Ù…Ù† Ø§Ù„Ù€ API |
| [`docs/FRONTEND_HANDOFF.md`](docs/FRONTEND_HANDOFF.md) | Ø¯Ù„ÙŠÙ„ Ù…Ø·ÙˆÙ‘Ø± Ø§Ù„ÙˆØ§Ø¬Ù‡Ø©: Ø§Ù„Ù…ØµØ§Ø¯Ù‚Ø©ØŒ OTPØŒ Ø§Ù„Ø±ÙØ¹ØŒ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„ØªØ¬Ø±ÙŠØ¨ÙŠØ© |
| [`docs/ERD_TO_API_MAP.md`](docs/ERD_TO_API_MAP.md) | Ø®Ø±ÙŠØ·Ø© ÙƒÙ„ Ø¬Ø¯ÙˆÙ„ â†’ Ù†Ù‚Ø·Ø© Ù†Ù‡Ø§ÙŠØ© |
| [`docs/DEVIATIONS.md`](docs/DEVIATIONS.md) | Ø§Ù„Ø§Ø®ØªÙ„Ø§ÙØ§Øª Ø§Ù„Ù…Ù‚ØµÙˆØ¯Ø© Ø¹Ù† Ø§Ù„Ù€ master prompt |
| [`docs/CHANGELOG.md`](docs/CHANGELOG.md) | ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¹Ù‚Ø¯ |
| [`docs/OPEN_QUESTIONS.md`](docs/OPEN_QUESTIONS.md) | Ø§Ù„Ù‚Ø±Ø§Ø±Ø§Øª Ø§Ù„Ø§ÙØªØ±Ø§Ø¶ÙŠØ© |
| [`docs/IMPLEMENTATION_ROADMAP.md`](docs/IMPLEMENTATION_ROADMAP.md) | Ø®Ø±ÙŠØ·Ø© Ø§Ù„Ù…Ø±Ø§Ø­Ù„ |
| [`docs/RUNBOOK.md`](docs/RUNBOOK.md) | Ø®Ø·ÙˆØ§Øª Ø§Ù„Ø¨Ù†Ø§Ø¡ ÙˆØ§Ù„ØªØ´ØºÙŠÙ„ ÙˆØ§Ù„ØªØ­Ù‚Ù‚ |
| [`docs/DEMO_CHECKLIST.md`](docs/DEMO_CHECKLIST.md) | ØªØ­Ø¶ÙŠØ± ÙˆØ¹Ø±Ø¶ Ø§Ù„Ù…Ù†Ø§Ù‚Ø´Ø© |

---

## Ø§Ù„Ø®ØµÙˆØµÙŠØ© ÙˆØ§Ù„Ø£Ø³Ø±Ø§Ø±

- **Ù„Ø§ secrets ÙÙŠ Ø§Ù„Ù…Ø³ØªÙˆØ¯Ø¹**: `appsettings.json` ÙŠÙØ´Ø­Ù† Ø¨Ù‚ÙŠÙ… ÙØ§Ø±ØºØ©ØŒ ÙˆÙ…Ù„ÙØ§Øª Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª Ø§Ù„Ø­Ù‚ÙŠÙ‚ÙŠØ©
  (`appsettings.Development.json`ØŒ `appsettings.Production.json`ØŒ `appsettings.*.Local.json`)
  Ù…ØªØ¬Ø§Ù‡ÙŽÙ„Ø© ÙÙŠ `.gitignore`.
- **ÙØ­Øµ Ø¢Ù„ÙŠ**: Ù…Ù‡Ù…Ø© `no-secrets` ÙÙŠ Ø§Ù„Ù€ CI ØªÙØ´Ù„ Ø¥Ù† ÙˆÙØ¬Ø¯ Ø£ÙŠ credential Ø­Ù‚ÙŠÙ‚ÙŠ ÙÙŠ Ø§Ù„Ø´Ø¬Ø±Ø©.
- **Ø¨Ù„Ø§ PII ÙÙŠ Ø§Ù„Ù„ÙˆØ¬**: Ù„Ø§ ØªÙÙƒØªØ¨ Ø§Ù„Ø·Ù„Ø¨Ø§Øª ÙˆÙ„Ø§ Ø§Ù„Ø¨Ø±ÙŠØ¯ ÙˆÙ„Ø§ Ø§Ù„Ù‡Ø§ØªÙ ÙˆÙ„Ø§ Ø§Ù„ØªÙˆÙƒÙ†Ø§Øª ÙÙŠ Ù…Ù„ÙØ§Øª Ø§Ù„Ù„ÙˆØ¬.

---

## ðŸ§± Ø¨Ù†ÙŠØ© Ø§Ù„Ù…Ø´Ø±ÙˆØ¹

```
src/
â”œâ”€â”€ HamzetWasl.Domain/          Ø§Ù„ÙƒÙŠØ§Ù†Ø§Øª + Ù‚ÙˆØ§Ø¹Ø¯ Ø§Ù„Ø¹Ù…Ù„ (Ø¨Ù„Ø§ Ø§Ø¹ØªÙ…Ø§Ø¯ÙŠØ§Øª Ø®Ø§Ø±Ø¬ÙŠØ©)
â”œâ”€â”€ HamzetWasl.Application/     Ø§Ù„Ø¹Ù‚ÙˆØ¯ (DTOs) + Ø§Ù„ÙˆØ§Ø¬Ù‡Ø§Øª + Ø§Ù„Ù€ Validators
â”œâ”€â”€ HamzetWasl.Infrastructure/  DbContextØŒ Ø§Ù„Ø®Ø¯Ù…Ø§ØªØŒ IdentityØŒ Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§ØªØŒ Ø¨Ø°Ø± Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª
â””â”€â”€ HamzetWasl.Api/             Controllers + Middleware + OpenAPI + SignalR
tests/
â”œâ”€â”€ HamzetWasl.UnitTests/
â””â”€â”€ HamzetWasl.IntegrationTests/
docs/                           Ø§Ù„ØªÙˆØ«ÙŠÙ‚ + openapi.json + logo
tools/                          Ø³ÙƒØ±Ø¨ØªØ§Øª Ù…Ø³Ø§Ø¹Ø¯Ø©
```

---

## ðŸ“„ Ù…Ù„Ø§Ø­Ø¸Ø§Øª

- **Smart App Control**: Ø¹Ù„Ù‰ Ø¨Ø¹Ø¶ Ø£Ø¬Ù‡Ø²Ø© Windows ÙŠÙ…Ù†Ø¹ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ù€ DLL ØºÙŠØ± Ø§Ù„Ù…ÙˆÙ‚Ù‘Ø¹ (`0x800711C7`).
  Ø¹Ø·Ù‘Ù„Ù‡ Ù…Ù† *Windows Security â†’ Device performance & security â†’ Smart App Control* Ø£Ùˆ Ø´ØºÙ‘Ù„
  Ø§Ù„Ø§Ø®ØªØ¨Ø§Ø±Ø§Øª Ø¹Ù„Ù‰ CI. Ø§Ù„Ø¨Ù†Ø§Ø¡ Ù„Ø§ ÙŠØªØ£Ø«Ø±.
- **HTTPS**: `UseHttpsRedirection` Ù…ÙØ¹Ù‘Ù„ØŒ ÙˆÙÙŠ Ø§Ù„Ø¥Ù†ØªØ§Ø¬ ÙØ¹Ù‘Ù„ HSTS Ø¹Ù„Ù‰ Ø§Ù„Ù€ proxy.
- **CORS**: Ø§Ù„Ø£ØµÙˆÙ„ Ø§Ù„Ù…Ø³Ù…ÙˆØ­Ø© Ø§ÙØªØ±Ø§Ø¶ÙŠØ§Ù‹ `http://localhost:3000` Ùˆ `http://localhost:5173`.
- **Ø£Ø³Ù„ÙˆØ¨ Ø§Ù„Ù€ commits**: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`).

</div>
