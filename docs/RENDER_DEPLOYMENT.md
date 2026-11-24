# Render + Flutter deployment checklist

الحالة: تجهيز ملفات فقط، مفيش رفع أو إنشاء حساب أو قاعدة بيانات. SQL Server زي ما هو، ومفيش تحويل لـ PostgreSQL أو تغيير في الـ Migrations.

## 1. المشروع

- Solution: `HamzetWasl.slnx` في جذر الريبو، مش ملف `.sln` قديم.
- Target framework: `.NET 10` (`net10.0`). مفيش `global.json` بيحدد SDK patch؛ التحقق هنا استخدم SDK `10.0.404`.
- Clean Architecture: `Api`, `Application`, `Domain`, `Infrastructure`.
- Main project: `src/HamzetWasl.Api/HamzetWasl.Api.csproj`.
- Provider unchanged: `Microsoft.EntityFrameworkCore.SqlServer` `10.0.14`.
- `WebApplication.CreateBuilder(args)` بيقرأ environment variables افتراضيا، و`GetConnectionString("DefaultConnection")` مستخدمة بالفعل في Infrastructure وstartup.

## 2. قاعدة البيانات، لازم تتجهز قبل الرفع

Render مفيهوش managed SQL Server ضمن قواعد البيانات المعروضة. PostgreSQL بتاعة Render مش بديل مباشر ومش هتشتغل مع المشروع ده. محتاج SQL Server أو Azure SQL Database متاحة من سيرفر Render، مش LocalDB على جهازك.

اختيارات مجانية موثقة بتاريخ 23 نوفمبر 2026، مش موارد اتعملت بالفعل:

- **MonsterASP.NET**: الخطة المجانية فيها database واحدة SQL Server بسعة 1 GB، بدون كارت. تفعيل Remote access من لوحة التحكم واستخدام public hostname مطلوب للاتصال من Render. مفيش automatic backups على الخطة المجانية. لازم نختبر صلاحيات الـ bootstrap والاتصال الفعلي قبل اعتبارها متوافقة.
  - https://www.monsterasp.net/MSSQL-Hosting/
  - https://help.monsterasp.net/databases/mssql/remote-access
- **Azure SQL Database free tier**: لكل قاعدة 100,000 vCore-seconds و32 GB data و32 GB backup شهريا، حتى 10 قواعد لكل subscription، حسب الأهلية. اختار **Auto-pause at free limit**، مش paid overages. لازم تتأكد إن العرض متاح فعلا في اشتراكك والمنطقة المختارة، وتراجع صفر التكلفة قبل الإنشاء. Azure for Students بيدعم التسجيل بدون كارت وبرصيد $100 لمدة 12 شهر، وStudents Starter مش مؤهل لعرض SQL المجاني. تسجيل Microsoft كان متعطل في المحاولة السابقة؛ التجهيز الحالي مش حل للتسجيل.
  - https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer
  - https://azure.microsoft.com/en-us/free/students

### ليه مضفناش Migrate()؟

المشروع **مفيهوش EF migrations** أصلا. الموجود هو `SchemaBootstrapper` بيطبق SQL schema المضمنة لإنشاء جداول المشروع على database فاضية، وبعده `IdentitySchemaInitializer` وجداول Identity والـ seeder. سيبنا المسار ده زي ما هو عشان يحقق هدف إنشاء الجداول أوتوماتيك، بدون اختراع history جديدة أو تغيير نوع الداتابيز. `Migrate()` لوحدها مش هتعمل جداول المشروع هنا، وخلطها بعدين مع schema موجودة من غير baseline ممكن يسبب تضارب.

مهم: الـ bootstrap الحالي بيفتح اتصال بقاعدة `master` للتأكد من وجود database، وبعدين ينشئها لو ناقصة. الاستضافات المشتركة وAzure SQL ممكن تمنع الخطوة دي للمستخدم الحالي. أنشئ قاعدة فاضية من لوحة الاستضافة أولا، وتحقق من صلاحيات `master` وDDL والجداول. مجرد إن معاك connection string مش دليل إن startup هيكمل. مغيرناش كود الـ DB أو صلاحياته في المهمة دي. لا تستخدم `SkipDatabaseInitialization=true` لتجاوز الخطأ إلا لو كل schema وIdentity والseed المطلوبة اتجهزت واتحققت منها بشكل منفصل.

## 3. إعداد Render

بعد ما ترفع الملفات بنفسك وتختار قاعدة SQL متوافقة:

| Setting | Value |
| --- | --- |
| Type | Web Service |
| Language / Runtime | Docker |
| Branch | `chore/render-docker` بعد رفعه |
| Root Directory | فاضي، سيبه blank |
| Dockerfile Path | `./Dockerfile` |
| Docker Build Context | `.` لو الحقل ظاهر |
| Docker Command | فاضي، استخدم ENTRYPOINT |
| Plan | Free |
| Health Check Path | `/health` |

الـ Dockerfile في جذر الريبو جنب الـ solution، multi-stage بـ .NET 10، restore للـ API وكل references، publish Release، runtime غير root على 8080. ملف `.dockerignore` بيستبعد build outputs وملفات local secrets وruntime uploads.

**8080 اختيارنا ومضبوط في Dockerfile، مش بورت إجباري من Render.** Render افتراضيا بيستخدم `PORT=10000`، فاضبط `PORT=8080` للتطابق.

## 4. Environment variables في Render Dashboard

لا تكتب أسرار في Git أو في الشات. خزّنها في إعدادات Render فقط.

| Key | Value / purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `8080` |
| `ASPNETCORE_URLS` | `http://+:8080`، نفس Dockerfile |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string كاملة من الاستضافة |
| `Jwt__SigningKey` | secret مستقل، 32 حرف على الأقل |
| `Storage__SigningKey` | secret مختلف، 32 حرف على الأقل |
| `Otp__HashKey` | secret مختلف، 32 حرف على الأقل |
| `Cors__AllowAll` | `true` للـ demo، ده default في الكود |
| `Swagger__Enabled` | `true` للـ demo، ده default في الكود حتى Production |
| `HttpsRedirection__Enabled` | `false` على Render فقط |

Connection string شكلها، ده template مش بيانات جاهزة:

```text
Server=tcp:<SQL_HOST>,1433;Database=<DB_NAME>;User ID=<DB_USER>;Password=<DB_PASSWORD>;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True
```

استخدم القيم واسم السيرفر والشهادة المعتمدين من مزود قاعدة البيانات. متستخدمش `postgres://` أو LocalDB أو Windows trusted authentication في Linux container. خليك على نفس signing keys بين deployments عشان tokens وsigned URLs ما تتكسرش.

إعدادات اختيارية حسب استخدامك:

- `Jwt__Issuer=HamzetWasl` و`Jwt__Audience=HamzetWasl.Clients` موجودين بالفعل كـ defaults، مش أسرار.
- `Seed__AdminEmail`, `Seed__AdminPhone`, `Seed__AdminPassword`, `Seed__AdminName`: لأول admin. من غير email/password الـ admin seed بيتخطى الإنشاء، مفيش حساب admin افتراضي مضمون. امسح password seed من إعدادات النشر بعد التحقق من إنشاء الحساب، مش من كلمة سر الحساب نفسه. عند وجود admin، يتم بذر مواعيد مقابلات مبدئية تلقائياً (`GET /api/v1/interview-slots`) إذا لم توجد مواعيد متاحة.
- `Otp__FixedCode`: (اختياري للـ Demo فقط، مثلاً `123456`) لتثبيت كود الـ OTP في بيئة التجربة لأن إرسال SMS/Email حالياً log-only. اتركه فارغاً في الإنتاج الحقيقي حتى تكون الأكواد عشوائية.
- `Google__ClientId`: لو هتستخدم Google sign-in، استخدم الـ client ID المطابق لتطبيقك.
- `Cors__AllowAll=false` + `Cors__AllowedOrigins__0=https://<YOUR_WEB_ORIGIN>` لإغلاق wildcard origin. زود `__1` وهكذا لو محتاج.
- `Swagger__Enabled=false` يقفل Swagger UI وJSON في كل البيئات.
- `Storage__RootPath=App_Data/uploads`, `Storage__PublicBaseUrl=/media`: موجودين كـ defaults، ما تغيرهمش في Render Free باعتبارهم تخزين دائم.

CORS المفتوح يسمح لأي موقع browser يعمل requests، وSwagger بيكشف وصف الـ API للناس، لكن ولا واحد منهم بيلغي authentication؛ استخدمهم للـ demo واقفلهم أو حدد origins قبل استخدام حقيقي. Flutter Android/iOS مش بيتقيد بـ browser CORS؛ السياسة مطلوبة أساسا لـ Flutter Web.

Render بيعمل HTTPS termination وHTTP-to-HTTPS redirect على الـ edge، والاتصال الداخلي للـ container HTTP. عشان كده بنقفل redirect داخل ASP.NET على Render، من غير توسيع الثقة لأي forwarded header مجهول. إعداد الـ proxy الحالي للـ client IP لم يتغير؛ client-IP based rate limits محتاجة مراجعة مع topology النشر الفعلية.

## 5. التحقق

Commands اللي اتنفذت بنجاح:

```bash
dotnet build HamzetWasl.slnx -c Release -p:TreatWarningsAsErrors=true
dotnet test tests/HamzetWasl.UnitTests/HamzetWasl.UnitTests.csproj -c Release --no-build
dotnet test tests/HamzetWasl.IntegrationTests/HamzetWasl.IntegrationTests.csproj -c Release --no-build
dotnet publish src/HamzetWasl.Api/HamzetWasl.Api.csproj -c Release --no-restore -o out/render /p:UseAppHost=false
```

- Build: **0 warnings, 0 errors**.
- Unit: **143/143 passed**.
- Integration: **45/45 passed** على SQL Server 2022 حقيقية محلية مؤقتة، الـ 41 الأصلية + 4 اختبارات Render جديدة.
- Publish: passed. مسار output المحلي وقت الاختبار كان temporary؛ `out/render` هنا مسار مناسب عندك.
- Docker build: **غير متحقق** لأن Docker executable/daemon مش متاحين في بيئة الفحص. اختبره عندك أو راجع Render build logs قبل اعتبار النشر ناجح.
- مفيش تغيير في controllers أو endpoints أو DTOs أو `docs/openapi.json`، ومفيش تغيير في SQL schema أو package references.

لو عندك Docker:

```bash
docker build -t hamzetwasl-render .
```

بعد النشر الفعلي اختبر HTTPS root و`/health` و`/swagger/index.html`، وجرب login من Flutter ببيانات test. `/health` بيعمل DB check؛ نجاحه أهم من مجرد ظهور الصفحة الرئيسية. أول startup بيعمل schema/seed وقد يتأخر مع SQL serverless. مفيش live URL اتعمل في المهمة دي؛ استخدم العنوان الحقيقي اللي Render يعرضه، وbase API path في Flutter هو `/api/v1`.

## 6. أوامر Git، تنفذها إنت فقط

لو بتطبق الـ patch على clone بتاعك وهو عند commit `e0e23bf65a624729d8841108b1030dfaa641cf94` أو نسخة متوافقة، ومن جذر الريبو:

```bash
git status
git switch -c chore/render-docker
git apply --check /path/to/hamzet-wasl-render.patch
git apply /path/to/hamzet-wasl-render.patch
git diff --check
git diff
git add Dockerfile .dockerignore src/HamzetWasl.Api/Program.cs tests/HamzetWasl.IntegrationTests/RenderConfigurationTests.cs docs/RENDER_DEPLOYMENT.md
git commit -m "Prepare SQL Server API for Render Docker and Flutter"
git push -u origin chore/render-docker
```

لو الـ branch موجودة عندك بالفعل استخدم `git switch chore/render-docker` بدل إنشائها. ما تعملش force push. لو `git apply --check` فشل، وقف وراجع الاختلافات بدل overwrite لشغلك. بعد push اختار الفرع نفسه في Render؛ مفيش merge لـ main اتعمل.

## 7. حدود الـ demo، مش production كاملة

- Render Free بينام بعد 15 دقيقة inactivity، وأول request ممكن ياخد حوالي دقيقة.
- الملفات المرفوعة والصور والـ logs الحالية local filesystem وبتضيع عند restart/redeploy/spin-down. قاعدة SQL الخارجية لا تجعل uploads دائمة. محتاج object storage أو persistent storage في مهمة منفصلة.
- Email/SMS الحاليين log-only، مش إرسال حقيقي؛ OTP ممكن يظهر في logs. ما تستخدمش بيانات ناس حقيقية أو تعتمد عليهم لتسجيل مستخدمين حقيقيين قبل تجهيز providers ومراجعة logging.
- Free service: 750 instance-hours/workspace/month. الاستخدام الزائد للـ bandwidth/build minutes ممكن يتفوتر لو ضفت payment method؛ من غيره بعض الخدمات/builds بتتعطل عند الحد. متضيفش كارت أو ترقي خطة على أساس إن كل حاجة هتفضل مجانية تلقائيا.
- Render Free Postgres، لو رجعت تفكر فيه، بينتهي بعد 30 يوم ومش متوافق مع الكود ده حاليا.

مصادر Render الرسمية:

- https://render.com/docs/docker
- https://render.com/docs/web-services
- https://render.com/docs/free
