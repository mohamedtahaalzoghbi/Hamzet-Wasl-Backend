<div align="center">
  <img src="docs/logo.svg" alt="Hamzet Wasl logo" width="120" height="120">
</div>

# همزة وصل — Hamzet Wasl Backend

<div align="center">

**منصة تربط ذوي الهمم بالمتطوعين والجمعيات والشركات**

`REST API` · `.NET 10` · `SQL Server` · `Clean Architecture` · `OpenAPI 3.0.4`

[![Tests](https://img.shields.io/badge/tests-172%20passing-12A4A4?style=flat-square)](https://github.com/)
[![Build](https://img.shields.io/badge/build-0%20warnings-0F4C81?style=flat-square)](https://github.com/)
[![OpenAPI](https://img.shields.io/badge/OpenAPI-3.0.4-12A4A4?style=flat-square)](docs/openapi.json)

</div>

---

<div dir="rtl">

## نظرة عامة

**منصة تمكّن ذوي الهمم من إيجاد الدعم والعمل، وتربطهم بالمتطوعين والجمعيات والشركات التي
تقدّم فرصاً وخدمات. هذا المستودع يحتوي **واجهة الـ Backend** (المرحلة الأولى
مكتملة): مصادقة JWT، ملفات شخصية، إعدادات إتاحة، تحقّق المتطوعين، دورة حياة المنظمات،
لوحة إدارة، إشعارات لحظية عبر SignalR، و62 جدولاً في مخطط مرجعي واحد.

</div>

---

## التقنيات المستخدمة

| الطبقة | التقنية | ملاحظة |
|---|---|---|
| Runtime | **.NET 10** (`net10.0`) | LTS، مع `Nullable` و`ImplicitUsings` |
| Web API | **ASP.NET Core** | Controllers + Middleware + Filters |
| Architecture | **Clean Architecture** | `Api → Application → Domain` و `Infrastructure → Application → Domain` |
| Database | **SQL Server** | 62 جدولاً في `Hamzet_Wasl_Schema_v2.sql` (بدون EF migrations) |
| ORM | Entity Framework Core 10 | المخطط SQL هو المصدر الوحيد للحقيقة |
| Auth | ASP.NET Core Identity + JWT Bearer | Access 15 دقيقة · Refresh 30 يوم مع تدوير |
| Validation | FluentValidation 12 | رسائل عربية + أكواد أخطاء ثابتة |
| Docs | Swashbuckle (OpenAPI 3.0.4) | مُصدَّر آلياً إلى `docs/openapi.json` |
| Realtime | SignalR | إشعارات لحظية على `/hubs/notifications` |
| Logging | Serilog | Console + File، بدون بيانات شخصية |
| Tests | **xUnit** + FluentAssertions + `WebApplicationFactory` | **131** اختبار وحدة + **41** اختبار تكامل |
| CI | GitHub Actions | بناء + اختبارات + فحص تطابق OpenAPI + فحص أسرار |

---

## ⚙️ التشغيل (Setup) لمطوّر جديد

### المتطلبات

| الأداة | الإصدار |
|---|---|
| .NET SDK | `10.0.x` |
| SQL Server | LocalDB (للتطوير) أو SQL Server 2019+ |
| sqlcmd / SSMS | اختياري (لتشغيل سكربتات المخطط) |

### 1) استنساخ المشروع

```bash
git clone https://github.com/<your-account>/Hamzet-Wasl-Backend.git
cd Hamzet-Wasl-Backend
```

### 2) تشغيل سكربت قاعدة البيانات

المخطط المرجعي هو `Hamzet_Wasl_Schema_v2.sql` (62 جدولاً + قيود CHECK و Foreign keys).
يمكن تشغيله بسطر واحد:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Schema_v2.sql
```

> **بديل مختصر:** لو تركت الـ API يقلع على قاعدة بيانات فارغة، سيطبّق السكربت **تلقائياً**
> (السكربت مُضمَّن داخل المشروع ولا توجد EF Core migrations). الخطوات 2 و3 مطلوبة فقط
> إن كانت لديك قاعدة بيانات قديمة.

### 3) تشغيل سكربتات الترقية (Migrations) للقاعدة القديمة

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_1_review_created_at.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HamzetWasl -i Hamzet_Wasl_Migration_v2_2_geo_tables.sql
```

كلاهما **idempotent** (يعمل بدون أخطاء إن طُبّق مرتين) ويحتوي على rollback موثّق.

### 4) ضبط الإعدادات (مهم)

المستودع **لا يحتوي أي أسرار**. كل قيمة حساسة تأتي من متغيرات البيئة أو من ملف محلي
لا يُرفع إلى Git:

```powershell
# انسخ ملف القالب إلى ملف محلي (المجلد مُتجاهَل في .gitignore)
copy src\HamzetWasl.Api\appsettings.Development.example.json src\HamzetWasl.Api\appsettings.Development.json
```

ثم عدّل القيم داخل `appsettings.Development.json`:

| المفتاح | الوصف |
|---|---|
| `Jwt:SigningKey` | مفتاح توقيع التوكن (32 حرفاً على الأقل) |
| `Storage:SigningKey` | مفتاح توقيع روابط الملفات (32 حرفاً على الأقل) |
| `Otp:HashKey` | مفتاح hash لأكواد OTP (16 حرفاً على الأقل) |
| `Seed:AdminPassword` | كلمة مرور أول حساب أدمن |

> نسخة جاهزة للاستخدام المحلي (غيّرها قبل أي نشر مشترك):
> `Jwt:SigningKey = DEV-ONLY-local-jwt-key-change-me-0123456789` ·
> `Storage:SigningKey = DEV-ONLY-local-storage-key-change-me-0123456789` ·
> `Otp:HashKey = DEV-ONLY-local-otp-key-change-me-0123456789` ·
> `Seed:AdminPassword = Admin@12345`

بديل للإعدادات: متغيرات البيئة (`Jwt__SigningKey`، `Storage__SigningKey`، `Otp__HashKey`) أو
`dotnet user-secrets`. للإنتاج استخدم `src/HamzetWasl.Api/appsettings.Production.example.json`
كقالب + أسرار من مخزن أسرار.

### 5) التشغيل

```bash
dotnet restore HamzetWasl.slnx
dotnet run --project src/HamzetWasl.Api
```

| الرابط | الغرض |
|---|---|
| `http://localhost:5153/swagger` | **Swagger UI** (بيئة التطوير فقط) |
| `http://localhost:5153/swagger/v1/swagger.json` | مستند OpenAPI |
| `http://localhost:5153/health` | فحص الصحة |
| `http://localhost:5153/hubs/notifications` | SignalR (يتطلب Bearer token) |

عند أول تشغيل: إنشاء القاعدة ← تطبيق المخطط ← إنشاء جداول Identity ← بذر البيانات المرجعية
(الأدوار، المهارات، المحافظات، المناطق) وحساب الأدمن.

### 6) (اختياري) حسابات العرض

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\seed-demo-accounts.ps1
```

ينشئ حسابات متطوع وجمعية وشركة وذوي همم بكلمة مرور `Demo@12345` وبملفات جاهزة للعرض.

---

## 🧪 تشغيل الاختبارات

```bash
# كل الاختبارات (172 اختباراً)
dotnet test HamzetWasl.slnx -c Release

# أو كل مجموعة على حدة
dotnet test tests/HamzetWasl.UnitTests/HamzetWasl.UnitTests.csproj -c Release
dotnet test tests/HamzetWasl.IntegrationTests/HamzetWasl.IntegrationTests.csproj -c Release
```

| المجموعة | العدد | ماذا تغطي |
|---|---|---|
| Unit | **131** | قواعد العمل، الـ validators، تطبيع الهاتف، قواعد الملفات، التوكنات، توقيتات UTC، حساب الـ preset، الكتالوج الجغرافي، **تطابق المخطط** |
| Integration | **41** | تدفقات حقيقية عبر HTTP على SQL Server حقيقي (تسجيل، رفع، مراجعة، إشعارات، صلاحيات، حد معدل، طلبات حذف) |

اختبارات التكامل تنشئ قاعدة `HamzetWasl_IT_<guid>` مؤقتة وتطبّق المخطط المرجعي كما هو ثم تحذفها.
على جهاز بدون LocalDB:

```bash
export HW_TEST_SQL_SERVER=localhost,1433
export HW_TEST_SQL_USER=sa
export HW_TEST_SQL_PASSWORD="<your-password>"
```

---

## 👥 أدوار المستخدمين

| الدور | كيف يُنشأ | أبرز المسارات |
|---|---|---|
| `disabled_user` | تسجيل عام | `/me/profile`, `/me/accessibility`, الإشعارات، طلب الحذف |
| `volunteer` | تسجيل عام | `/me/volunteer-profile`, `/me/volunteer-location`, `/me/verification`, `/me/availability`, `/me/training` |
| `association` | تسجيل باسم منظمة | `/me/organization` (ملف، شعار، غلاف، مستندات، تقديم، مراجعة) |
| `company` | تسجيل باسم شركة | نفس صلاحيات `association` |
| `admin` | بذر تلقائي فقط (غير مسجَّل) | `/admin/volunteer-reviews`, `/admin/organization-reviews`, `/admin/interview-slots`, `/admin/deletion-requests`, `/admin/audit` |

---

## 📚 التوثيق

| الملف | المحتوى |
|---|---|
| [`docs/API_CONTRACT.md`](docs/API_CONTRACT.md) | العقد الكامل لكل نقطة نهاية |
| [`docs/openapi.json`](docs/openapi.json) | مستند OpenAPI 3.0.4 مُصدَّر من الـ API |
| [`docs/FRONTEND_HANDOFF.md`](docs/FRONTEND_HANDOFF.md) | دليل مطوّر الواجهة: المصادقة، OTP، الرفع، البيانات التجريبية |
| [`docs/ERD_TO_API_MAP.md`](docs/ERD_TO_API_MAP.md) | خريطة كل جدول → نقطة نهاية |
| [`docs/DEVIATIONS.md`](docs/DEVIATIONS.md) | الاختلافات المقصودة عن الـ master prompt |
| [`docs/CHANGELOG.md`](docs/CHANGELOG.md) | تاريخ العقد |
| [`docs/OPEN_QUESTIONS.md`](docs/OPEN_QUESTIONS.md) | القرارات الافتراضية |
| [`docs/IMPLEMENTATION_ROADMAP.md`](docs/IMPLEMENTATION_ROADMAP.md) | خريطة المراحل |
| [`docs/RUNBOOK.md`](docs/RUNBOOK.md) | خطوات البناء والتشغيل والتحقق |
| [`docs/DEMO_CHECKLIST.md`](docs/DEMO_CHECKLIST.md) | تحضير وعرض المناقشة |

---

## الخصوصية والأسرار

- **لا secrets في المستودع**: `appsettings.json` يُشحن بقيم فارغة، وملفات الإعدادات الحقيقية
  (`appsettings.Development.json`، `appsettings.Production.json`، `appsettings.*.Local.json`)
  متجاهَلة في `.gitignore`.
- **فحص آلي**: مهمة `no-secrets` في الـ CI تفشل إن وُجد أي credential حقيقي في الشجرة.
- **بلا PII في اللوج**: لا تُكتب الطلبات ولا البريد ولا الهاتف ولا التوكنات في ملفات اللوج.

---

## 🧱 بنية المشروع

```
src/
├── HamzetWasl.Domain/          الكيانات + قواعد العمل (بلا اعتماديات خارجية)
├── HamzetWasl.Application/     العقود (DTOs) + الواجهات + الـ Validators
├── HamzetWasl.Infrastructure/  DbContext، الخدمات، Identity، الإعدادات، بذر البيانات
└── HamzetWasl.Api/             Controllers + Middleware + OpenAPI + SignalR
tests/
├── HamzetWasl.UnitTests/
└── HamzetWasl.IntegrationTests/
docs/                           التوثيق + openapi.json + logo
tools/                          سكربتات مساعدة
```

---

## 📄 ملاحظات

- **Smart App Control**: على بعض أجهزة Windows يمنع تحميل الـ DLL غير الموقّع (`0x800711C7`).
  عطّله من *Windows Security → Device performance & security → Smart App Control* أو شغّل
  الاختبارات على CI. البناء لا يتأثر.
- **HTTPS**: `UseHttpsRedirection` مفعّل، وفي الإنتاج فعّل HSTS على الـ proxy.
- **CORS**: الأصول المسموحة افتراضياً `http://localhost:3000` و `http://localhost:5173`.
- **أسلوب الـ commits**: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`).

</div>
