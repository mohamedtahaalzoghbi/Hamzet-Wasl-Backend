# Hamzet Wasl — Backend API (Phase 1)

<div dir="rtl">

**همزة وصل** — واجهة برمجية (REST API) لمنصة تربط ذوي الهمم بالمتطوعين والجمعيات والشركات:
الملف التعريفي، إعدادات الإتاحة، معجزة التحقق للمتطوعين، دورة حياة خاصة بالمنظمات، ولوحة إدارة كاملة.

| | |
|---|---|
| الإصدار الحالي | `v1` (المرحلة الأولى مكتملة) |
| Base path | `/api/v1` |
| العقد الموثّق | `docs/openapi.json` (OpenAPI 3.0.4) + `docs/API_CONTRACT.md` |
| قاعدة البيانات | SQL Server (62 جدولاً في المخطط المرجعي) |
| الاختبارات | 131 اختبار وحدة + 41 اختبار تكامل — ناجحة بالكامل |
| البناء | 0 Warning / 0 Error (`TreatWarningsAsErrors=true`) |
| آخر commit | `d931077` — UI & ERD alignment (batches 2-4) |

</div>

---

## 1. المحتويات

- [نظرة عامة](#2-نظرة-عامة)
- [التقنيات المستخدمة](#3-التقنيات-المستخدمة)
- [بنية المشروع (Clean Architecture)](#4-بنية-المشروع-clean-architecture)
- [خطوات التشغيل](#5-خطوات-التشغيل)
- [تشغيل الاختبارات](#6-تشغيل-الاختبارات)
- [أدوار المستخدمين](#7-أدوار-المستخدمين)
- [الحسابات التجريبية](#8-الحسابات-التجريبية)
- [اصطلاحات الـ API](#9-اصطلاحات-ال-api)
- [التوثيق](#10-التوثيق)
- [ملاحظات تشغيلية](#11-ملاحظات-تشغيلية)

---

## 2. نظرة عامة

<div dir="rtl">

### ما الذي ينجزه الـ API؟

| المجال | الوظائف |
|---|---|
| **المصادقة** | تسجيل، دخول، Google، تحديث التوكن، خروج، نسيان وإعادة تعيين كلمة المرور، تغيير كلمة المرور، تحقق البريد والهاتف عبر OTP، جلسة المستخدم |
| **البيانات المرجعية** | أنواع الحسابات، المحافظات والمناطق (من قاعدة البيانات)، مجالات العمل، نطاقات عدد الموظفين، المهارات، الاهتمامات، فئات الإتاحة، وسائل الراحة |
| **الملف الشخصي** | الملف الشخصي، الصورة الشخصية، الاهتمامات، المهارات، طلب حذف الحساب، مركز الإشعارات (سجل + تعليم المقروء) |
| **الإتاحة** | حفظ إعدادات الإتاحة، دعم فئة أو أكثر (حتى 3 فئات)، وحساب الـ `effectivePreset` الفعلي الذي يطبقه العميل |
| **المتطوع** | ملف المتطوع، الموقع الجغرافي، معجزة التحقق (البريد ثم الهاتف ثم 3 مستندات ثم رابط تواصل ثم حجز مقابلة)، أوقات التفرغ، التدريب |
| **المنظمة** | ملف المنظمة (جمعية أو شركة)، الشعار والغلاف، المستندات الرسمية، التقديم للمراجعة، حالة المراجعة |
| **الإدارة** | طوابير مراجعة المتطوعين والمنظمات، القرارات، إدارة مواعيد المقابلات، تعليق وتفعيل المستخدمين، طلبات حذف الحساب، سجل التدقيق |
| **الملفات** | الوصول للملفات الخاصة عبر رابط موقّع ومحدود الصلاحية |
| **الفورية** | إشعارات لحظية عبر SignalR على `/hubs/notifications` |

### قرارات معمارية جوهرية

- **المخطط هو المصدر الوحيد للحقيقة**: ملف `Hamzet_Wasl_Schema_v2.sql` مُضمَّن داخل الـ Infrastructure
  ويقوم التطبيق بتطبيقه تلقائياً على أي قاعدة بيانات فارغة. **لا توجد EF Core migrations** — سكربت SQL
  واحد يمنع وجود مصدرين للحقيقة.
- **اختبار تطابق المخطط**: اختبار وحدة يقارن نموذج EF مع المخطط، واختبار تكامل يتحقق من الجداول
  والـ CHECK constraints داخل قاعدة بيانات حقيقية.
- **عقد API واحد**: غلاف استجابة موحّد، تسمية camelCase، توقيتات ISO-8601 UTC، معرّفات GUID،
  أكواد أخطاء إنجليزية ثابتة ورسائل عربية.
- **لا تغيير في العقد دون توثيق**: كل تعديل يمر عبر `docs/CHANGELOG.md` و`docs/DEVIATIONS.md`
  ويفحص الـ CI أن `docs/openapi.json` مطابق للـ API الفعلي.

</div>

---

## 3. التقنيات المستخدمة

| الطبقة | التقنية | الإصدار / الملاحظة |
|---|---|---|
| Runtime | .NET | `net10.0` |
| Web | ASP.NET Core Web API | Controllers + Middleware + Filters |
| ORM | Entity Framework Core (SQL Server) | `10.0.14` — بدون migrations |
| المصادقة | ASP.NET Core Identity + JWT Bearer | Access 15 دقيقة / Refresh 30 يوم |
| التحقق | FluentValidation | `12.2.0` |
| توثيق الـ API | Swashbuckle (OpenAPI 3.0.4) | يُصدَّر آلياً إلى `docs/openapi.json` |
| الفورية | SignalR | Hub واحد للإشعارات |
| اللوجز | Serilog | Console + File، بدون بيانات شخصية ولا روابط الطلبات |
| التخزين المؤقت | `IMemoryCache` + StackExchange.Redis جاهز | الكتالوج الجغرافي 10 دقائق |
| اختبارات الوحدة | xUnit + FluentAssertions | 131 اختبار |
| اختبارات التكامل | xUnit + `WebApplicationFactory` + LocalDB | 41 اختبار |
| CI | GitHub Actions | بناء + اختبارات + فحص تطابق OpenAPI + فحص الأسرار |

---

## 4. بنية المشروع (Clean Architecture)

```
HamzetWasl.slnx
├── src/
│   ├── HamzetWasl.Domain/          ← الكيانات + قواعد العمل (لا يعتمد على أي طبقة أخرى)
│   │   ├── Entities/               ← UserAccount, VolunteerProfile, Organization, ...
│   │   ├── Constants/              ← الأدوار، الحالات، فئات الإتاحة، أنماط الإشعارات
│   │   └── Rules/                  ← AccessibilityPresets, AccessibilityOverrides, FileRules, ...
│   ├── HamzetWasl.Application/     ← العقود (DTOs) + الواجهات + الـ Validators
│   │   ├── Contracts/              ← Auth, Profile, Volunteer, Organization, Admin, Meta
│   │   ├── Interfaces/             ← IAuthService, IGeoCatalog, IFileStorage, ...
│   │   ├── Validation/             ← Validators.cs
│   │   └── Metadata/               ← MetaCatalog (كتالوج احتياطي) + DTOs
│   ├── HamzetWasl.Infrastructure/  ← التنفيذ: DbContext, الخدمات, Identity, الإعدادات
│   │   ├── Persistence/            ← HamzetWaslDbContext, DatabaseSeeder, SchemaBootstrapper
│   │   ├── Services/               ← AuthService, VolunteerService, OrganizationService, ...
│   │   ├── Identity/               ← TokenService, AppUser
│   │   └── SchemaComparison/       ← SqlSchemaParser (اختبار تطابق المخطط)
│   └── HamzetWasl.Api/             ← Controllers + Middleware + OpenAPI + SignalR
├── tests/
│   ├── HamzetWasl.UnitTests/       ← قواعد العمل، الـ validators، تطابق المخطط
│   └── HamzetWasl.IntegrationTests/← تدفقات حقيقية على SQL Server
├── docs/                           ← التوثيق + openapi.json
├── tools/                          ← سكربتات مساعدة (seed-demo-accounts.ps1)
├── Hamzet_Wasl_Schema_v2.sql       ← المخطط المرجعي (62 جدولاً)
├── Hamzet_Wasl_Migration_v2_1_review_created_at.sql
└── Hamzet_Wasl_Migration_v2_2_geo_tables.sql
```

<div dir="rtl">

قاعدة الاعتماد: `Api → Application → Domain` و `Infrastructure → Application → Domain`.
طبقة الـ Domain لا تعرف شيئاً عن EF Core أو ASP.NET، وطبقة الـ Api لا تحوي أي منطق عمل.

</div>

---

## 5. خطوات التشغيل

### 5.1 المتطلبات

| الأداة | الإصدار | ملاحظات |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --info` يجب أن يعرض `net10.0` |
| SQL Server LocalDB | MSSQLLocalDB | يأتي مع Visual Studio أو كحزمة مستقلة |
| sqlcmd / SSMS | اختياري | مطلوب فقط لسكربتات الـ Migration وكشف الأخطاء |
| Postman / Insomnia | اختياري | Swagger UI مدمج داخل الـ API |

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### 5.2 التشغيل السريع

```powershell
git clone <repo-url>
cd <project-folder>
dotnet restore HamzetWasl.slnx
dotnet run --project src/HamzetWasl.Api
```

عند أول تشغيل يقوم التطبيق تلقائياً بـ:

1. إنشاء قاعدة البيانات `HamzetWasl` إن لم تكن موجودة.
2. تطبيق `Hamzet_Wasl_Schema_v2.sql` كاملاً (62 جدولاً).
3. إنشاء جداول ASP.NET Core Identity.
4. بذر الأدوار والبيانات المرجعية والمحافظات والمناطق والتدريب وحساب الأدمن.

| الرابط | الغرض |
|---|---|
| `http://localhost:5153/swagger` | Swagger UI (بيئة التطوير فقط) |
| `http://localhost:5153/swagger/v1/swagger.json` | مستند OpenAPI |
| `http://localhost:5153/health` | فحص الصحة |
| `http://localhost:5153/hubs/notifications` | SignalR (يتطلب Bearer token) |

> منفذ `5153` يأتي من `src/HamzetWasl.Api/Properties/launchSettings.json` ويمكن تغييره من هناك.

### 5.3 قاعدة بيانات موجودة مسبقاً

<div dir="rtl">

سقوط السكربت يعمل **على قاعدة بيانات فارغة فقط**. إذا كان لديك قاعدة بيانات من قبل، نفّذ السكربتين
بالترتيب (كلاهما idempotent ويحتوي على rollback):

</div>

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_1_review_created_at.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_2_geo_tables.sql
```

<div dir="rtl">

ثم شغّل السكربت التجريبي (اختياري لكنه موصى به قبل أي عرض أو تسليم للواجهة):

</div>

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\seed-demo-accounts.ps1
```

### 5.4 الإعدادات

<div dir="rtl">

لا توجد مفاتيح احتياطية داخل الكود: التطبيق **يرفض الإقلاع** إذا غابت مفاتيح التوقيع. استخدم متغيرات
البيئة أو ملف `appsettings.{Environment}.json`:

</div>

| المفتاح | مطلوب | الوصف |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | نعم | سلسلة اتصال SQL Server |
| `Jwt__SigningKey` | نعم | 32 حرفاً على الأقل (HS256) |
| `Storage__SigningKey` | نعم | 32 حرفاً على الأقل (توقيع روابط الملفات) |
| `Otp__HashKey` | نعم | 16 حرفاً على الأقل (HMAC لأكواد OTP) |
| `Seed__AdminEmail` / `Seed__AdminPassword` | اختياري | حساب الأدمن الأول |
| `Cors__AllowedOrigins__0` | اختياري | أصول الـ CORS (افتراضياً `localhost:3000` و `localhost:5173`) |
| `RateLimiting__*` | اختياري | حدود تحديد المعدل |
| `SkipDatabaseInitialization` | اختياري | `true` لتخطي التهيئة (تستخدمه الاختبارات) |

---

## 6. تشغيل الاختبارات

```powershell
# 1) بناء نظيف (التحذيرات تُعامل كأخطاء)
dotnet build HamzetWasl.slnx -c Release -p:TreatWarningsAsErrors=true

# 2) اختبارات الوحدة (سريعة، بدون قاعدة بيانات)
dotnet test tests/HamzetWasl.UnitTests/HamzetWasl.UnitTests.csproj -c Release --no-build

# 3) اختبارات التكامل (قاعدة بيانات حقيقية)
dotnet test tests/HamzetWasl.IntegrationTests/HamzetWasl.IntegrationTests.csproj -c Release --no-build
```

| suite | العدد | ماذا تغطي |
|---|---|---|
| Unit | **131** | قواعد العمل، الـ validators، تطبيع الهاتف، قواعد الملفات، التوكنات، التوقيتات UTC، حساب الـ preset، الكتالوج الجغرافي، **تطابق المخطط** |
| Integration | **41** | تدفقات حقيقية عبر HTTP: تسجيل ودخول، التوكنات، ملف الإتاحة، تحقق المتطوع، المنظمات والمراجعة، الإشعارات، الصلاحيات، الرفع، حد المعدل، طلبات الحذف، ترتيب المراجعات |

<div dir="rtl">

اختبارات التكامل تنشئ قاعدة بيانات مؤقتة `HamzetWasl_IT_<guid>`، تطبّق المخطط المرجعي كما هو،
تشغّل الـ API الحقيقي عبر `WebApplicationFactory`، ثم تحذف القاعدة. على جهاز بدون LocalDB مرّر المتغيرات:

</div>

```powershell
$env:HW_TEST_SQL_SERVER   = "localhost,1433"
$env:HW_TEST_SQL_USER     = "sa"
$env:HW_TEST_SQL_PASSWORD = "Password123!"
```

---

## 7. أدوار المستخدمين

<div dir="rtl">

| الدور | كيف يُنشأ | أبرز ما يصل إليه |
|---|---|---|
| `disabled_user` | تسجيل عام | `/me/profile`, `/me/interests`, `/me/skills`, **`/me/accessibility`** (قراءة وحفظ وسحب الموافقة), الإشعارات, طلب حذف الحساب |
| `volunteer` | تسجيل عام | `/me/profile`, **`/me/volunteer-profile`**, **`/me/volunteer-location`**, **`/me/verification`** (الأدلة وروابط التواصل وحجز وتعديل وإلغاء مقابلة), `/interview-slots`, **`/me/availability`**, `/me/training`, الإشعارات, طلب حذف الحساب |
| `association` | تسجيل عام باسم منظمة | `/me/profile`, **`/me/organization`** (الملف والشعار والغلاف والمستندات والتقديم وحالة المراجعة), الإشعارات, طلب حذف الحساب |
| `company` | تسجيل عام باسم شركة | نفس صلاحيات `association` (النوع `company` يُخزَّن في `ORGANIZATION.organization_type`) |
| `admin` | **بذر تلقائي فقط** — غير قابل للتسجيل العام | `/admin/volunteer-reviews`, `/admin/organization-reviews`, قرارات المراجعة, `/admin/interview-slots`, تعليق المستخدمين, `/admin/deletion-requests`, `/admin/audit` |

ملاحظات:

- الأدمن **غير مسجَّل** عبر `/api/v1/auth/register` — الطلب يُرفض برمز 400 على الحقل `role`.
- حسابات الجمعيات والشركات تبدأ بحالة `pending_approval` وتتحول إلى `active` عند موافقة الأدمن.
- كل مستخدم مُصادَق عليه يصل إلى مسارات `/me/*` العامة (الملف، الاهتمامات، المهارات، الإشعارات، طلب الحذف).

</div>

---

## 8. الحسابات التجريبية

<div dir="rtl">

| الدور | البريد | كلمة المرور | الحالة | كيف أُنشئ |
|---|---|---|---|---|
| `admin` | `admin@hamzetwasl.local` | `Admin@12345` | `active` | بذر تلقائي من `Seed:*` |
| `volunteer` | `demo.volunteer@hamzetwasl.local` | `Demo@12345` | `active` | `tools/seed-demo-accounts.ps1` |
| `association` | `demo.association@hamzetwasl.local` | `Demo@12345` | `pending_approval` | `tools/seed-demo-accounts.ps1` |
| `company` | `demo.company@hamzetwasl.local` | `Demo@12345` | `pending_approval` | `tools/seed-demo-accounts.ps1` |
| `disabled_user` | `demo.disabled@hamzetwasl.local` | `Demo@12345` | `active` | `tools/seed-demo-accounts.ps1` |

السكربت يعمل بعد تشغيل الـ API: يسجّل الحسابات عبر الـ API العام، ويعلّم البريد والهاتف كـ *موثّق*
مباشرة في قاعدة البيانات (ناقل البريد والرسالة في التطوير لا يطبع الكود عمداً)، ويملأ ملفاً بسيطاً
لكل حساب حتى تكون الواجهة جاهزة للعرض.

</div>

---

## 9. اصطلاحات الـ API

| Concern | القاعدة |
|---|---|
| المسار | كل المسارات تحت `/api/v1` |
| الغلاف | كل رد (نجاح أو خطأ) داخل: `{ success, data, error, traceId }` |
| JSON | أسماء الخصائص camelCase، والـ enums نصوص snake_case |
| المعرّفات | GUID بصيغة lowercase-hyphenated |
| التواريخ | ISO-8601 UTC دائماً مع اللاحقة `Z` |
| الهاتف | E.164 (`+201012345678`)، والإدخال يقبل `01xxxxxxxxx` ويُطبَّع في الخادم |
| الترقيم | `?page=1&pageSize=20` (الحد الأقصى 100) |
| الأخطاء | `error.code` إنجليزي ثابت + `error.message` عربي + `error.fields` لأخطاء الحقول |
| المصادقة | `Authorization: Bearer <accessToken>` |
| `204` | جسم فارغ بدون غلاف |
| الملفات | `GET /api/v1/files` يعيد الملف الثنائي وليس JSON |

```json
{
  "success": true,
  "data": { "userId": "…", "role": "volunteer", "accessToken": "…" },
  "error": null,
  "traceId": "0HN7…"
}
```

---

## 10. التوثيق

| الملف | المحتوى |
|---|---|
| `docs/API_CONTRACT.md` | العقد الكامل لكل نقطة نهاية (المرجع الأساسي للواجهة) |
| `docs/openapi.json` | مستند OpenAPI 3.0.4 مُصدَّر من الـ API نفسه (فحص التطابق في CI) |
| `docs/FRONTEND_HANDOFF.md` | **دليل مطوّر الواجهة**: استخدام OpenAPI، المصادقة، OTP، الرفع، بيانات الدخول |
| `docs/ERD_TO_API_MAP.md` | خريطة كل جدول ERD إلى نقطة النهاية |
| `docs/DEVIATIONS.md` | كل اختلاف مقصود عن الـ master prompt مع السبب والأثر |
| `docs/CHANGELOG.md` | تاريخ العقد وكل التغييرات |
| `docs/OPEN_QUESTIONS.md` | القرارات الافتراضية المقابلة لأسئلة الـ prompt |
| `docs/IMPLEMENTATION_ROADMAP.md` | خريطة المراحل والجاهزية |
| `docs/RUNBOOK.md` | خطوات البناء والتشغيل والتحقق والإطلاق |
| `docs/DEMO_CHECKLIST.md` | قائمة تحضير وعرض المناقشة |

---

## 11. ملاحظات تشغيلية

<div dir="rtl">

- **Smart App Control**: على بعض أجهزة Windows يمنع Smart App Control تحميل الـ DLL غير الموقّع
  (`0x800711C7`). عطّله من *Windows Security → Device performance & security → Smart App Control*،
  أو شغّل الاختبارات على CI. عملية البناء لا تتأثر.
- **Swagger** يعمل في بيئة التطوير فقط (`Swagger:Enabled` أو `ASPNETCORE_ENVIRONMENT=Development`).
- **HTTPS**: `UseHttpsRedirection` مفعّل، وفي الإنتاج يجب تفعيل HSTS على الـ proxy.
- **CORS**: الأصول المسموحة افتراضياً `http://localhost:3000` و `http://localhost:5173` — أضف منفذ
  تطبيقك في `Cors:AllowedOrigins`.
- **لا أسرار في المستودع**: فحص CI يرفض أي مفتاح حقيقي غير معلَّم بـ `DEV-ONLY`.

</div>

---

<div dir="rtl">

### أسلوب الـ commits: **Conventional Commits** (`feat:`, `fix:`, `docs:`, `test:`)، وكل دفعة = مراجعة واحدة قابلة للتشغيل.

</div>
