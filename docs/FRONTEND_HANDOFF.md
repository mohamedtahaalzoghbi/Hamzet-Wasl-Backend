# Frontend Handoff — Hamzet Wasl API (Phase 1)

<div dir="rtl">

هذا المستند موجّه لمطوّر الواجهة الأمامية (Flutter / React / Web). هدفه أن تبدأ العمل
التطبيقي فوراً دون الرجوع إلى الكود.

**المصادر الرسمية للـ API:**

| الملف | متى تستخدمه |
|---|---|
| `docs/openapi.json` | توليد الأنواع وعميل الـ API، أو استيراده في Postman / Insomnia |
| `docs/API_CONTRACT.md` | السلوك الدقيق لكل نقطة نهاية (القواعد، الأخطاء، الحالات الحدّية) |
| `docs/ERD_TO_API_MAP.md` | ربط أي حقل في الواجهة بالجدول الذي مصدره |
| `docs/DEVIATIONS.md` | الحالات التي خالفت الـ prompt (مع السبب) |

> أي تغيير في `openapi.json` يعني تغييراً في العقد. لو لاحظت فرقاً بين الـ API وما تستخدمه،
> راجع `docs/CHANGELOG.md` أولاً — كل تغيير موثّق ومقروء.

</div>

---

## 1. تشغيل الـ API محلياً

```powershell
dotnet run --project src/HamzetWasl.Api
```

| | |
|---|---|
| Base URL (محلي) | `http://localhost:5153/api/v1` |
| Swagger UI | `http://localhost:5153/swagger` |
| OpenAPI JSON | `http://localhost:5153/swagger/v1/swagger.json` |
| Health | `http://localhost:5153/health` |
| SignalR hub | `http://localhost:5153/hubs/notifications` |

<div dir="rtl">

الـ API ينشئ قاعدة البيانات ويطبّق المخطط ويزرع البيانات المرجعية وحساب الأدمن تلقائياً عند أول تشغيل.
إن كان لديك CORS مخصص أضف منفذ تطبيقك في `src/HamzetWasl.Api/appsettings.json` →
`Cors:AllowedOrigins` (الافتراضي `http://localhost:3000` و `http://localhost:5173`).

</div>

---

## 2. استخدام `openapi.json`

### 2.1 استيراد مباشر

| الأداة | الطريقة |
|---|---|
| Swagger UI | افتح `/swagger` واستخدم زر *Authorize* وضع الـ access token |
| Postman | `Import → Link` بالمسار `/swagger/v1/swagger.json` (أو ملف `docs/openapi.json`) |
| Insomnia | `Import → OpenAPI` من نفس الملف |
| VS Code (Thunder Client / REST Client) | `Import → OpenAPI` |

### 2.2 توليد عميل API وأنواع

```bash
# TypeScript / React
npx openapi-typescript docs/openapi.json -o src/api/schema.d.ts
npx openapi-fetch docs/openapi.json -o src/api/client.ts

# عميل كامل (TypeScript / Dart / Kotlin / Swift)
openapi-generator-cli generate -i docs/openapi.json -g typescript-fetch -o ./generated
openapi-generator-cli generate -i docs/openapi.json -g dart -o ./generated
```

<div dir="rtl">

**مهم بعد التوليد:**

- Base path `/api/v1` **ليس** جزءاً من الـ paths، أضفه بنفسك في الـ base URL.
- كل استجابة JSON ملفوفة داخل الغلاف `{ success, data, error, traceId }` — استخدم `data` فقط.
- نقاط النهاية التي تعيد `204` ترجع **فارغة**؛ لا تحاول قراءة JSON منها.
- `GET /api/v1/files` يعيد **ملفاً ثنائياً** وليس JSON.

</div>

### 2.3 قراءة الـ schemas

| العنصر | كيف يظهر في `openapi.json` | ماذا يعني لك |
|---|---|---|
| `needType`, `needTypes`, `role`, `theme`, `status`… | `enum` | استخدم generated union types ولا تكتب strings حرة |
| `pageSize` | `minimum: 1`, `maximum: 100` | مرّر قيمة ضمن المدى |
| `needTypes` | `array` من 1 إلى 3 | تحقق أن `needTypes[0] === needType` قبل الإرسال |
| `manualOverrides` | `object` حر | حقل JSON من ملكية العميل، وحدّه 4 KB |
| التواريخ | `format: date-time` | كلها UTC تنتهي بـ `Z` |
| الـ ids | `format: uuid` | `Guid` بصيغة lowercase |

### 2.4 مخطط الأمان

المستند يعرّف `bearerAuth` (JWT). بعد استيراده إلى Postman اختر Bearer Token وضع الـ access token
(بدون كلمة `Bearer` — Postman يضيفها).

---

## 3. اصطلاحات الاستدعاء

<div dir="rtl">

### الغلاف

</div>

```json
{ "success": true,  "data": { }, "error": null, "traceId": "0HN7..." }
{ "success": false, "data": null,  "error": { "code": "VALIDATION_ERROR", "message": "…", "fields": { } }, "traceId": "0HN7..." }
```

<div dir="rtl">

| القاعدة | التفصيل |
|---|---|
| الفرع على `error.code` | لا تعتمد على `error.message` (عربي وقابل للتغيير) |
| `error.fields` | خريطة اسم الحقل → قائمة رسائل (تحقق من المدخلات) |
| `traceId` | أظهره في شاشة الأخطاء و copied مع البلاغ |
| الترقيم | `?page=1&pageSize=20`، والرد: `{ items, page, pageSize, totalCount }` |
| `204` | لا يوجد غلاف ولا JSON |
| الحالة `422` | الطلب صحيح لكن لا ينطبق (مثل `PROFILE_INCOMPLETE`) |

</div>

### أمثلة كود

```ts
// TypeScript
async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = store.accessToken;
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init.headers ?? {}),
    },
  });

  if (res.status === 204) return undefined as T;

  const body = await res.json();
  if (!body.success) {
    const error = new ApiError(body.error.code, body.error.message, body.error.fields);
    error.status = res.status;
    error.traceId = body.traceId;
    throw error;
  }
  return body.data as T;
}
```

```dart
// Dart / Flutter
final res = await dio.post(
  '/api/v1/auth/login',
  data: {'emailOrPhone': email, 'password': password},
);
final data = (res.data as Map)['data'] as Map;
```

---

## 4. المصادقة (Bearer Token)

<div dir="rtl">

### تدفق الجلسة

</div>

```
POST /api/v1/auth/register | /auth/login
        → 200 { userId, role, accountStatus, email, emailVerified, phone, phoneVerified,
                displayName, accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt }

حفظ accessToken + refreshToken  →  إضافة Authorization: Bearer <accessToken> في كل طلب

انتهى Access Token (15 دقيقة)؟  →  POST /api/v1/auth/refresh { refreshToken }
        → 200 بزوج جديد من التوكنات  (والتوكن القديم يصبح غير صالح فوراً)

إنهاء الجلسة                  →  POST /api/v1/auth/logout { refreshToken }  →  204
```

<div dir="rtl">

**نقاط مهمة:**

- **توكن الـ Refresh لا يعمل كـ Bearer.** attempting استخدامه في طلب محمي يعيد 401 `UNAUTHORIZED`.
  هذا قيد أمني مقصود (توكن مخصص لكل غرض).
- **دوران التوكن**: كل `refresh` يُبطل التوكن السابق. لو ردّ الـ API بـ 401 على `/auth/refresh`،
  أعد تسجيل الدخول.
- `POST /auth/logout` يبطل كل جلسات المستخدم الحالية.
- `GET /api/v1/auth/session` يعيد الدور والحالة وحالة التحقق بدون أي طلب تعديل.
- **تخزين التوكنات**: على الويب استخدم `httpOnly` cookie أو `sessionStorage`، ولا تضع الـ access token
  في `localStorage` إلا إذا كان التطبيق لا يدعم البدائل الآمنة.
- عند تسجيل الخروج امسح التوكنات محلياً حتى لو فشل الطلب.

</div>

### الطلب المحمي

```http
GET /api/v1/me/profile HTTP/1.1
Host: localhost:5153
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

---

## 5. بيانات الدخول التجريبية

<div dir="rtl">

| الدور | البريد | كلمة المرور | الحالة | ملاحظات |
|---|---|---|---|---|
| `admin` | `admin@hamzetwasl.local` | `Admin@12345` | `active` | بذر تلقائي؛ **غير قابل للتسجيل** |
| `volunteer` | `demo.volunteer@hamzetwasl.local` | `Demo@12345` | `active` | ملف متطوع جاهز (القاهرة / مدينة نصر) |
| `association` | `demo.association@hamzetwasl.local` | `Demo@12345` | `pending_approval` | ينشئ منظمة بحالة `pending` |
| `company` | `demo.company@hamzetwasl.local` | `Demo@12345` | `pending_approval` | ينشئ منظمة بحالة `pending` |
| `disabled_user` | `demo.disabled@hamzetwasl.local` | `Demo@12345` | `active` | إعدادات إتاحة محفوظة بفئتين |

> **كلمة مرور الأدمن** (`Admin@12345`) هي القيمة المحلية المقترحة في ملف الإعدادات المحلي
> (`appsettings.Development.json` المنسوخ من القالب). المستودع العام لا يحتوي أي credential:
> كل مطوّر يختار كلمة مروره في `Seed:AdminPassword` قبل أول تشغيل.

### كيف تُنشأ هذه الحسابات؟

</div>

```powershell
# 1) شغّل الـ API
dotnet run --project src/HamzetWasl.Api

# 2) في نافذة أخرى: أنشئ حسابات العرض
powershell -ExecutionPolicy Bypass -File .\tools\seed-demo-accounts.ps1
```

<div dir="rtl">

السكربت يسجّل الحسابات عبر الـ API العام (نفس ما يفعله العميل)، ثم يعلّم البريد والهاتف كـ *موثّق*
في قاعدة البيانات، ويملأ ملفاً بسيطاً لكل حساب. تشغيله مرة أخرى آمن (سجل الدخول بدلاً من التسجيل).

**لماذا المنظمتان `pending_approval`؟** لأن هذا هو الوضع الحقيقي بعد التسجيل: ترفع المستندات
والشعار ثم تقدّم للمراجعة، ويوافق الأدمن فتصبح `active`. هذه حالة ممتازة لعرض شاشة
"الأوراق ناقصة / بانتظار المراجعة" في المناقشة.

</div>

### تسجيل حساب جديد من الواجهة

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "role": "volunteer",
  "name": "أحمد مصطفى",
  "email": "ahmed@example.com",
  "phone": "01012345678",
  "password": "Passw0rd!",
  "confirmPassword": "Passw0rd!",
  "termsAccepted": true,
  "policyVersion": "1.0"
}
```

<div dir="rtl">

- `role` المسموح عند التسجيل العام: `disabled_user` | `volunteer` | `association` | `company`.
  قيمة `admin` تُرفض بـ 400 على الحقل `role`.
- حسابات الجمعيات والشركات ترسل `organizationName` بدلاً من `name`.
- الهاتف يقبل `01xxxxxxxxx` ويُخزَّن E.164 (`+201012345678`).
- التسجيل يعيد توكنات فوراً؛ التحقق من البريد/الهاتف خطوة لاحقة.

</div>

---

## 6. ملاحظات مهمة عن OTP

<div dir="rtl">

### النقاط

| البند | القيمة |
|---|---|
| طول الكود | 6 أرقام |
| مدة الصلاحية | 10 دقائق |
| المحاولات الخاطئة | 5 كحد أقصى ثم `OTP_TOO_MANY_ATTEMPTS` |
| مدة الانتظار بين كودين | 60 ثانية ثم `OTP_RESEND_TOO_SOON` |
| حد المعدل | 6 طلبات كل 10 دقائق لكل مستخدم |
| التخزين | hash فقط (HMAC) — لا يُحفظ الكود نصاً في قاعدة البيانات |

### نقاط النهاية

</div>

| Endpoint | Body | النتيجة |
|---|---|---|
| `POST /api/v1/auth/email/send-code` | — | 204 (يحل محل أي كود غير مُفعّل) |
| `POST /api/v1/auth/email/verify` | `{ "code": "123456" }` | 204 عند النجاح، 400/422 عند الفشل |
| `POST /api/v1/auth/phone/send-code` | `{ "phone": "01012345678" }` | 204 |
| `POST /api/v1/auth/phone/verify` | `{ "code": "123456" }` | 204 |

<div dir="rtl">

### سلوك مهم يجب أن تعرفه

1. **الرقم الجديد لا يُكتب قبل التحقق.** عند استدعاء `phone/send-code` برقم جديد يبقى الرقم القديم
   في الحساب، والرقم الجديد يبقى *معلّقاً* حتى ينجح التحقق. (`GET /auth/session` يعكس الحقيقة).
2. **في بيئة التطوير لا يُطبع الكود في اللوج.** ناقلات البريد والرسالة في التطوير تكتب "تم الإرسال"
   فقط (بدون رقم ولا كود) — سلوك أمني مقصود. لذلك:
   - **للتطوير والعرض استخدم الحسابات التجريبية** التي يوثّقها السكربت.
   - إن أردت اختبار تدفق OTP فعلياً، اربط ناقلاً حقيقياً (SES / Twilio) أو استخدم
     `CapturingMessageSender` الموجود في اختبارات التكامل.
3. الأخطاء المتوقعة: `OTP_INVALID` (400/422)، `OTP_EXPIRED` (422)، `OTP_TOO_MANY_ATTEMPTS` (422)،
   `OTP_RESEND_TOO_SOON` (429 مع `Retry-After`).

</div>

---

## 7. رفع الملفات (multipart/form-data)

<div dir="rtl">

كل نقاط رفع الملفات تقبل `multipart/form-data` فقط، وتتحقق من الامتداد ونوع المحتوى
**والبصمة الثنائية للملف** (magic bytes) — ملف مرفوع باسم `.jpg` ومحتوى نصي يُرفض بـ 400.

</div>

| Endpoint | الحقول | الأنواع | الحد |
|---|---|---|---|
| `PUT /api/v1/me/profile/avatar` | `file` | PNG / JPG | 5 MB |
| `PUT /api/v1/me/organization/logo` | `file` | PNG / JPG | 5 MB |
| `PUT /api/v1/me/organization/cover` | `file` | PNG / JPG | 5 MB |
| `PUT /api/v1/me/organization/documents/{documentType}` | `file` | PDF / PNG / JPG | 10 MB |
| `POST /api/v1/me/verification/evidence` | `evidenceType` + `file` | PNG / JPG | 10 MB |

<div dir="rtl">

- `documentType` ∈ `commercial_register` | `tax_card` | `address_proof` | `license`
  (الأول ثلاثة مطلوبة، `license` اختياري).
- `evidenceType` ∈ `national_id_front` | `national_id_back` | `face_photo`.
- حد الطلب نفسه = حد الملف + 1 MB؛ تجاوزه يعطي 413 `REQUEST_TOO_LARGE`.
- حد المعدل على الرفع: 30 طلباً في الساعة لكل مستخدم.
- الصور العامة (الصورة الشخصية، الشعار، الغلاف) ترجع رابطاً عاماً `/media/...` يمكن استخدامه مباشرة داخل وسم الصورة.
- المستندات الخاصة والأدلة ترجع **رابطاً موقّعاً**:
  `GET /api/v1/files?key=...&expires=...&signature=...` (صالح 10 دقائق، بلا توكن).

</div>

### أمثلة

```bash
curl -X PUT http://localhost:5153/api/v1/me/profile/avatar \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@avatar.png;type=image/png"

curl -X POST http://localhost:5153/api/v1/me/verification/evidence \
  -H "Authorization: Bearer $TOKEN" \
  -F "evidenceType=national_id_front" \
  -F "file=@front.jpg;type=image/jpeg"
```

```ts
// TypeScript
const body = new FormData();
body.append('evidenceType', 'national_id_front');
body.append('file', file);          // لا تضع Content-Type يدوياً
await fetch(`${BASE_URL}/me/verification/evidence`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}` },   // لا تضف Content-Type
  body,
});
```

```dart
// Dart / Flutter
final form = FormData.fromMap({
  'evidenceType': 'national_id_front',
  'file': await MultipartFile.fromFile(path, filename: 'front.jpg'),
});
await dio.post('/api/v1/me/verification/evidence', data: form,
    options: Options(headers: {'Authorization': 'Bearer $token'}));
```

<div dir="rtl">

**أخطاء الرفع:**

| الكود | الحالة | السبب |
|---|---|---|
| `FILE_TYPE_NOT_ALLOWED` | 400 | الامتداد أو نوع المحتوى أو البصمة غير مطابقة |
| `FILE_TOO_LARGE` | 413 | الملف أكبر من حد الفئة |
| `REQUEST_TOO_LARGE` | 413 | الطلب نفسه أكبر من الحد |

</div>

---

## 8. الإشعارات الفورية (SignalR)

```ts
const connection = new HubConnectionBuilder()
  .withUrl(`${BASE_URL.replace('/api/v1', '')}/hubs/notifications`, {
    accessTokenFactory: () => store.accessToken,
  })
  .build();

connection.on('notificationReceived', (payload) => {
  // { id, type, titleAr, bodyAr, createdAt, payloadJson }
});
await connection.start();
```

<div dir="rtl">

- اسم الطريقة المتفق عليه: **`notificationReceived`** (لا تتغير).
- على الويب (WebSocket/SSE) لا يمكن إرسال الترويسات، لذلك مرّر التوكن كمعامل في طلب negotiate:
  `?access_token=<token>` — الـ API يقبله.
- نفس الإشعارات متاحة كسجل عبر `GET /api/v1/me/notifications?page=1&pageSize=20`
  مع `PUT /api/v1/me/notifications/{id}/read` (idempotent، 204).
- `payloadJson` (نوع الإشعار ومعرّف الهدف) موجود **في الحدث الحي فقط**؛ السجل التاريخي لا يحمله
  (عمود `payload_json` مؤجل إلى Phase 2 — انظر `docs/DEVIATIONS.md` بند 19).

</div>

---

## 9. خريطة المسارات حسب الدور

<details>
<summary><b>عام (كل مستخدم مُصادَق عليه)</b></summary>

| Method | Path | ملاحظات |
|---|---|---|
| GET/PUT | `/me/profile` | `{displayName, bio, city, visibility}` |
| PUT | `/me/profile/avatar` | multipart |
| PUT | `/me/interests` | `{interestIds:[int]}` → 204 |
| PUT | `/me/skills` | `{skillIds:[int]}` → 204 |
| POST | `/me/deletion-request` | 201، و409 إذا كان هناك طلب pending |
| GET | `/me/notifications` | paginated |
| PUT | `/me/notifications/{id}/read` | 204 idempotent |

</details>

<details>
<summary><b>الإتاحة — <code>disabled_user</code> فقط</b></summary>

| Method | Path | ملاحظات |
|---|---|---|
| GET | `/me/accessibility` | 404 إن لم يُنشأ من قبل |
| PUT | `/me/accessibility` | upsert؛ `needTypes` من 1 إلى 3، الأول = `needType` |
| DELETE | `/me/accessibility` | سحب الموافقة → 204 |

الاستجابة تُرجع `needType` و `needTypes` و `effectivePreset` (القيم الفعلية التي يطبّقها العميل).

</details>

<details>
<summary><b>المتطوع</b></summary>

| Method | Path | ملاحظات |
|---|---|---|
| GET/PUT | `/me/volunteer-profile` | `helpMode`, `skillLevel`, `weeklyHours` (1-168), `skillIds` |
| GET/PUT | `/me/volunteer-location` | `governorate`, `serviceArea` من `/meta/governorates` |
| GET | `/me/verification` | حالة المعجزة وخطواتها (`currentStep`, `steps[]`, `canBookInterview`) |
| POST | `/me/verification/evidence` | multipart (3 مستندات) |
| PUT | `/me/verification/social-links` | `{links:[{platform,url}]}` |
| GET | `/interview-slots` | يتطلب توكن متطوع |
| POST/PUT/DELETE | `/me/verification/interview` | حجز / تعديل / إلغاء |
| GET/PUT | `/me/availability` | `slots[]` غير متداخلة في المستقبل |
| GET | `/me/training` | وحدات التدريب |
| POST | `/me/training/{moduleId}/attempts` | تسجيل محاولة |

</details>

<details>
<summary><b>المنظمة (جمعية / شركة)</b></summary>

| Method | Path | ملاحظات |
|---|---|---|
| GET/PUT | `/me/organization` | `{name, field, description, governorate, city, website, employeeCountRange, contactInformation}` |
| PUT | `/me/organization/logo` | multipart 5 MB |
| PUT | `/me/organization/cover` | multipart 5 MB |
| PUT | `/me/organization/documents/{documentType}` | multipart 10 MB |
| POST | `/me/organization/submit` | 422 إن نقصت الأوراق أو الملف |
| GET | `/me/organization/review-status` | `{approvalStatus, canSubmit, missingDocuments[]}` |

</details>

<details>
<summary><b>الإدارة</b></summary>

| Method | Path | ملاحظات |
|---|---|---|
| GET | `/admin/volunteer-reviews` | مرتبة بالأحدث (`created_at`) |
| POST | `/admin/volunteer-reviews/{id}/decision` | `notes` إلزامية عند الرفض |
| GET | `/admin/organization-reviews` | نفس المنطق |
| POST | `/admin/organization-reviews/{id}/decision` | |
| GET/POST/DELETE | `/admin/interview-slots` | إدارة المواعيد |
| PUT | `/admin/users/{userId}/suspension` | تعليق / تفعيل |
| GET | `/admin/deletion-requests` | طلبات حذف الحساب |
| POST | `/admin/deletion-requests/{id}/resolve` | تنفيذ الإخفاء (anonymisation) |
| GET | `/admin/audit` | سجل التدقيق |

</details>

<details>
<summary><b>البيانات المرجعية (عامة)</b></summary>

`GET /api/v1/meta/account-types` · `/meta/governorates` · `/meta/governorates/{key}/areas` ·
`/meta/business-fields` · `/meta/employee-ranges` · `/meta/skills` · `/meta/interests` ·
`/meta/need-types` · `/meta/accommodations`

</details>

---

## 10. حالات حدّية يجب أن تتعامل معها الواجهة

<div dir="rtl">

| الحالة | متى | ماذا تفعل |
|---|---|---|
| 401 + `INVALID_CREDENTIALS` | دخول خاطئ أو حساب مقفل | أظهر "بيانات الدخول غير صحيحة" (لا تفرق بين الخطأ والقفل) |
| 403 + `ACCESSIBILITY_NOT_ALLOWED` | دور غير `disabled_user` | اخفِ شاشة الإتاحة لهذا الدور |
| 403 + `FORBIDDEN` | دور غير مسموح بالمسار | اعرض شاشة "غير مصرح" |
| 404 + `NOT_FOUND` | مورد غير موجود أو غير مملوك | لا تكشف وجود مورد لغير المالك (خصوصاً الإشعارات) |
| 409 + `DELETION_REQUEST_ALREADY_PENDING` | طلب حذف قائم | اعرض حالة الطلب الحالي |
| 409 + `ORGANIZATION_ALREADY_SUBMITTED` | مراجعة بالفعل قيد الانتظار | اعرض `review-status` |
| 422 + `PROFILE_INCOMPLETE` | ناقص في ملف المنظمة | الرسالة تسرد النواقص — اعرضها |
| 422 + `DOCUMENTS_MISSING` | ناقص مستندات | `missingDocuments[]` |
| 422 + `VERIFICATION_INCOMPLETE` | خطوة تحقق ناقصة | `currentStep` يحدد الخطوة التالية |
| 413 | ملف أو طلب كبير | قلّل الحجم أو اعرض رسالة واضحة |
| 429 | حد المعدل | استخدم `Retry-After` في رسالة "حاول بعد قليل" |

</div>

---

## 11. قائمة جاهزية الواجهة

- [ ] تم استيراد `docs/openapi.json` في Postman / توليد الأنواع.
- [ ] تم ضبط الـ base URL على `/api/v1` وإضافة `Authorization: Bearer`.
- [ ] يوجد طبقة API واحدة تفك الغلاف (`data`) وتحوّل `error.code` إلى أخطاء تطبيق.
- [ ] تدوير التوكن عند 401 + إعادة المحاولة مرة واحدة فقط.
- [ ] جميع التواريخ تُعرض بتوقيت المستخدم المحلي من UTC.
- [ ] شاشة الإتاحة تدعم من 1 إلى 3 فئات وترسل `needTypes[0] === needType`.
- [ ] شاشة رفع الملفات ترسل `multipart/form-data` بدون ضبط `Content-Type` يدوياً.
- [ ] شاشات المحافظات والمناطق تقرأ من `/meta/governorates` (لا قائمة ثابتة في الكود).
- [ ] مركز الإشعارات يقرأ السجل ويحدّث المقروء، مع الاستماع لـ `notificationReceived`.
- [ ] كل شاشة خطأ تعرض `traceId` عند الحاجة للدعم.

</div>

---

## 12. حدود معروفة (Phase 1)

<div dir="rtl">

- `payloadJson` متاح في الحدث الحي فقط، وليس في سجل الإشعارات.
- لا توجد نقاط إدارة للمحافظات/المناطق بعد (تعديل الجداول يتم من SQL، والواجهة تقرأ فقط).
- `VERIFICATION_REVIEW` لا يحتوي `updated_at`؛ إعادة التقديم بعد الرفض تعيد استخدام نفس الصف.
- البريد/الرسالة الحقيقية غير مربوطة (ناقلات التطوير تكتب في اللوج فقط).
- الحذف يمر بطلب `deletion_request` يوافق عليه الأدمن؛ لا يوجد إلغاء للطلب في Phase 1.

</div>
