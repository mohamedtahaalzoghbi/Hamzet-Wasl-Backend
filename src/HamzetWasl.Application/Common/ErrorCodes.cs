namespace HamzetWasl.Application.Common;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string PhoneAlreadyExists = "PHONE_ALREADY_EXISTS";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";
    public const string AccountTypeRequired = "ACCOUNT_TYPE_REQUIRED";
    public const string EmailNotVerified = "EMAIL_NOT_VERIFIED";
    public const string PhoneNotVerified = "PHONE_NOT_VERIFIED";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpTooManyAttempts = "OTP_TOO_MANY_ATTEMPTS";
    public const string OtpResendTooSoon = "OTP_RESEND_TOO_SOON";
    public const string GoogleNotConfigured = "GOOGLE_NOT_CONFIGURED";
    public const string GoogleTokenInvalid = "GOOGLE_TOKEN_INVALID";
    public const string GoogleTicketInvalid = "GOOGLE_TICKET_INVALID";
    public const string VerificationIncomplete = "VERIFICATION_INCOMPLETE";
    public const string VerificationAlreadySubmitted = "VERIFICATION_ALREADY_SUBMITTED";
    public const string OrganizationNotApproved = "ORGANIZATION_NOT_APPROVED";
    public const string DocumentsMissing = "DOCUMENTS_MISSING";
    public const string ProfileIncomplete = "PROFILE_INCOMPLETE";
    public const string DocumentTypeInvalid = "DOCUMENT_TYPE_INVALID";
    public const string OrganizationAlreadySubmitted = "ORGANIZATION_ALREADY_SUBMITTED";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileTypeNotAllowed = "FILE_TYPE_NOT_ALLOWED";
    public const string InterviewSlotUnavailable = "INTERVIEW_SLOT_UNAVAILABLE";
    public const string InterviewAlreadyBooked = "INTERVIEW_ALREADY_BOOKED";
    public const string InvalidState = "INVALID_STATE";
    public const string SelfActionNotAllowed = "SELF_ACTION_NOT_ALLOWED";
    public const string AccessibilityNotAllowed = "ACCESSIBILITY_NOT_ALLOWED";
    public const string ConsentRequired = "CONSENT_REQUIRED";
    public const string RequestTooLarge = "REQUEST_TOO_LARGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
}

public static class ArabicMessages
{
    private static readonly IReadOnlyDictionary<string, string> Messages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ErrorCodes.ValidationError] = "بيانات الطلب غير صحيحة.",
            [ErrorCodes.Unauthorized] = "يجب تسجيل الدخول أولاً.",
            [ErrorCodes.Forbidden] = "ليس لديك صلاحية لتنفيذ هذا الإجراء.",
            [ErrorCodes.NotFound] = "العنصر المطلوب غير موجود.",
            [ErrorCodes.Conflict] = "تعارض في البيانات.",
            [ErrorCodes.EmailAlreadyExists] = "البريد الإلكتروني مستخدم بالفعل.",
            [ErrorCodes.PhoneAlreadyExists] = "رقم الهاتف مستخدم بالفعل.",
            [ErrorCodes.InvalidCredentials] = "البريد الإلكتروني أو كلمة المرور غير صحيحة.",
            [ErrorCodes.AccountSuspended] = "تم إيقاف هذا الحساب. تواصل مع الدعم.",
            [ErrorCodes.AccountTypeRequired] = "يجب اختيار نوع الحساب لإكمال التسجيل.",
            [ErrorCodes.EmailNotVerified] = "يجب تأكيد البريد الإلكتروني أولاً.",
            [ErrorCodes.PhoneNotVerified] = "يجب تأكيد رقم الهاتف أولاً.",
            [ErrorCodes.OtpExpired] = "انتهت صلاحية الرمز. اطلب رمزاً جديداً.",
            [ErrorCodes.OtpInvalid] = "الرمز غير صحيح، يُرجى المحاولة مرة أخرى.",
            [ErrorCodes.OtpTooManyAttempts] = "تم تجاوز عدد المحاولات المسموح. اطلب رمزاً جديداً.",
            [ErrorCodes.OtpResendTooSoon] = "انتظر قليلاً قبل طلب رمز جديد.",
            [ErrorCodes.GoogleNotConfigured] = "تسجيل الدخول بجوجل غير مُفعّل حالياً.",
            [ErrorCodes.GoogleTokenInvalid] = "تعذر التحقق من حساب جوجل.",
            [ErrorCodes.GoogleTicketInvalid] = "انتهت صلاحية جلسة التسجيل. ابدأ من جديد.",
            [ErrorCodes.VerificationIncomplete] = "لم تكتمل خطوات التحقق بعد.",
            [ErrorCodes.VerificationAlreadySubmitted] = "تم إرسال طلب التحقق بالفعل.",
            [ErrorCodes.OrganizationNotApproved] = "يجب اعتماد المؤسسة أولاً.",
            [ErrorCodes.DocumentsMissing] = "يجب رفع جميع المستندات المطلوبة.",
            [ErrorCodes.ProfileIncomplete] = "يرجى إكمال بيانات الملف التعريفي قبل الإرسال.",
            [ErrorCodes.DocumentTypeInvalid] = "نوع المستند غير صحيح.",
            [ErrorCodes.OrganizationAlreadySubmitted] = "تم إرسال طلب المؤسسة بالفعل.",
            [ErrorCodes.FileTooLarge] = "حجم الملف أكبر من المسموح.",
            [ErrorCodes.FileTypeNotAllowed] = "نوع الملف غير مسموح.",
            [ErrorCodes.InterviewSlotUnavailable] = "هذا الموعد غير متاح.",
            [ErrorCodes.InterviewAlreadyBooked] = "لديك موعد مقابلة بالفعل.",
            [ErrorCodes.InvalidState] = "لا يمكن تنفيذ هذا الإجراء في الحالة الحالية.",
            [ErrorCodes.SelfActionNotAllowed] = "لا يمكنك تنفيذ هذا الإجراء على نفسك.",
            [ErrorCodes.AccessibilityNotAllowed] = "إعدادات الإتاحة متاحة لحسابات ذوي الهمم فقط.",
            [ErrorCodes.ConsentRequired] = "يجب الموافقة على الشروط وسياسة الخصوصية.",
            [ErrorCodes.RequestTooLarge] = "حجم الطلب أكبر من المسموح.",
            [ErrorCodes.RateLimited] = "تم تجاوز عدد الطلبات المسموح. حاول لاحقاً.",
            [ErrorCodes.InternalError] = "حدث خطأ غير متوقع. حاول مرة أخرى."
        };

    public static string For(string code, string? fallback = null) =>
        Messages.TryGetValue(code, out var message) ? message : fallback ?? "حدث خطأ.";
}
