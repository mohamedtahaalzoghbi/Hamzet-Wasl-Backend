using FluentValidation;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Metadata;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Rules;

namespace HamzetWasl.Application.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("يجب اختيار نوع الحساب.")
            .Must(RoleNames.PublicRegistration.Contains).WithMessage("نوع الحساب غير صحيح.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب.")
            .EmailAddress().WithMessage("اكتب بريدًا إلكترونيًا صحيحًا مثل name@example.com.")
            .MaximumLength(256);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب.")
            .Must(p => PhoneNumberNormalizer.TryNormalize(p) is not null)
            .WithMessage("رقم الهاتف غير صحيح. استخدم رقم موبايل مصري مثل 01xxxxxxxxx.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون ٨ أحرف على الأقل.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("كلمتا المرور غير متطابقتين.");

        RuleFor(x => x.TermsAccepted)
            .Must(v => v).WithMessage("يجب الموافقة على الشروط والأحكام وسياسة الخصوصية.");

        When(x => RoleNames.IsOrganization(x.Role), () =>
        {
            RuleFor(x => x.OrganizationName)
                .NotEmpty().WithMessage("اسم المؤسسة مطلوب.")
                .MaximumLength(200);
        }).Otherwise(() =>
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("الاسم مطلوب.")
                .MaximumLength(200);
        });
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.EmailOrPhone).NotEmpty().WithMessage("أدخل البريد الإلكتروني أو رقم الهاتف.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("كلمة المرور مطلوبة.");
    }
}

public sealed class GoogleAuthRequestValidator : AbstractValidator<GoogleAuthRequest>
{
    public GoogleAuthRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("رمز جوجل مطلوب.");
        When(x => !string.IsNullOrWhiteSpace(x.Role), () =>
        {
            RuleFor(x => x.Role!)
                .Must(RoleNames.PublicRegistration.Contains).WithMessage("نوع الحساب غير صحيح.");
        });
        When(x => !string.IsNullOrWhiteSpace(x.Phone), () =>
        {
            RuleFor(x => x.Phone!)
                .Must(p => PhoneNumberNormalizer.TryNormalize(p) is not null)
                .WithMessage("رقم الهاتف غير صحيح.");
        });
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("رمز التحديث مطلوب.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("اكتب بريدًا إلكترونيًا صحيحًا.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("اكتب بريدًا إلكترونيًا صحيحًا.");
        RuleFor(x => x.Token).NotEmpty().WithMessage("رمز إعادة التعيين مطلوب.");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون ٨ أحرف على الأقل.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("كلمتا المرور غير متطابقتين.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة.");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).WithMessage("كلمة المرور الجديدة يجب أن تكون ٨ أحرف على الأقل.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("كلمتا المرور غير متطابقتين.");
    }
}

public sealed class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").WithMessage("رمز التحقق يجب أن يكون ٦ أرقام.");
    }
}

public sealed class SendPhoneCodeRequestValidator : AbstractValidator<SendPhoneCodeRequest>
{
    public SendPhoneCodeRequestValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب.")
            .Must(p => PhoneNumberNormalizer.TryNormalize(p) is not null)
            .WithMessage("رقم الهاتف غير صحيح.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(200)
            .WithMessage("الاسم المعروض يجب أن يكون بين حرفين و٢٠٠ حرف.");
        RuleFor(x => x.Bio).MaximumLength(4000).WithMessage("النبذة طويلة جدًا.");
        RuleFor(x => x.City).MaximumLength(200);
        RuleFor(x => x.Visibility).Must(ProfileVisibilities.All.Contains).WithMessage("إعداد الظهور غير صحيح.");
    }
}

public sealed class SetInterestsRequestValidator : AbstractValidator<SetInterestsRequest>
{
    public SetInterestsRequestValidator()
    {
        RuleFor(x => x.InterestIds).NotNull();
        RuleForEach(x => x.InterestIds).GreaterThan(0);
    }
}

public sealed class SetSkillsRequestValidator : AbstractValidator<SetSkillsRequest>
{
    public SetSkillsRequestValidator()
    {
        RuleFor(x => x.SkillIds).NotNull();
        RuleForEach(x => x.SkillIds).GreaterThan(0);
    }
}

public sealed class UpsertAccessibilityRequestValidator : AbstractValidator<UpsertAccessibilityRequest>
{
    public UpsertAccessibilityRequestValidator()
    {
        RuleFor(x => x.NeedType).Must(NeedTypes.All.Contains).WithMessage("نوع الاحتياج غير صحيح.");
        RuleFor(x => x.NeedTypes)
            .Must(v => v is null || v.Count is >= AccessibilityOverrides.MinNeedTypes and <= AccessibilityOverrides.MaxNeedTypes)
            .WithMessage("يجب اختيار فئة إتاحة واحدة على الأقل، وبحد أقصى ثلاث فئات.");
        RuleFor(x => x.NeedTypes)
            .Must(v => v is null || v.Distinct(StringComparer.Ordinal).Count() == v.Count)
            .WithMessage("لا يمكن تكرار فئة الإتاحة.");
        RuleFor(x => x.NeedTypes)
            .Must(v => v is null || v.All(item => item is not null && NeedTypes.All.Contains(item)))
            .WithMessage("نوع الاحتياج غير صحيح.");
        RuleFor(x => x).Custom((request, context) =>
        {
            // عند غياب الحقل يُعتبر الطلب فئة واحدة (الفئة الأساسية) للتوافق مع العملاء القدامى.
            if (request.NeedTypes is null || request.NeedTypes.Count == 0)
            {
                return;
            }

            if (!string.Equals(request.NeedTypes[0], request.NeedType, StringComparison.Ordinal))
            {
                context.AddFailure(nameof(request.NeedTypes),
                    "يجب أن تكون الفئة الأولى مطابقة لنوع الاحتياج الأساسي.");
            }
        });
        RuleFor(x => x.Theme).Must(AccessibilityThemes.All.Contains).WithMessage("السمة غير صحيحة.");
        RuleFor(x => x.FontScale).InclusiveBetween(1.0m, 2.0m).WithMessage("حجم الخط يجب أن يكون بين ١ و٢.");
        RuleFor(x => x.InteractionMode).Must(InteractionModes.All.Contains).WithMessage("طريقة التفاعل غير صحيحة.");
        RuleFor(x => x.NotificationMode).Must(NotificationModes.All.Contains).WithMessage("طريقة التنبيه غير صحيحة.");
        RuleFor(x => x.ManualOverrides).Must(v => !v.HasValue || BusinessRules.IsValidJson(v.Value.GetRawText()))
            .WithMessage("الإعدادات اليدوية يجب أن تكون JSON صحيحًا.");
        RuleFor(x => x.ManualOverrides)
            .Must(v => !v.HasValue || AccessibilityOverrides.IsWithinSizeLimit(v.Value.GetRawText()))
            .WithMessage("حجم الإعدادات اليدوية يجب ألا يتجاوز ٤ كيلوبايت.");
    }
}

public sealed class UpdateVolunteerProfileRequestValidator : AbstractValidator<UpdateVolunteerProfileRequest>
{
    public UpdateVolunteerProfileRequestValidator()
    {
        RuleFor(x => x.Introduction).MaximumLength(4000);
        RuleFor(x => x.ExperienceNotes).MaximumLength(4000);
        RuleFor(x => x.HelpMode).Must(v => v is HelpModes.OnlineOnly or HelpModes.OnlineAndField)
            .WithMessage("نوع المساعدة غير صحيح.");
        RuleFor(x => x.SkillLevel).Must(v => v is SkillLevels.Beginner or SkillLevels.Intermediate or SkillLevels.Advanced)
            .WithMessage("مستوى المهارة غير صحيح.");
        RuleFor(x => x.WeeklyHours).InclusiveBetween(1, 168).WithMessage("ساعات التطوع يجب أن تكون بين ١ و١٦٨.");
        RuleForEach(x => x.SkillIds).GreaterThan(0);
    }
}

public sealed class VolunteerLocationRequestValidator : AbstractValidator<VolunteerLocationRequest>
{
    public VolunteerLocationRequestValidator()
    {
        // التحقق من المحافظة/المنطقة انتقل إلى VolunteerService لأنه يعتمد على
        // الكتالوج الديناميكي في قاعدة البيانات (IGeoCatalog) وليس قائمة ثابتة.
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).When(x => x.Latitude.HasValue)
            .WithMessage("خط العرض غير صحيح.");
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).When(x => x.Longitude.HasValue)
            .WithMessage("خط الطول غير صحيح.");
        RuleFor(x => x.ServiceRadiusKm).InclusiveBetween(1, 200).When(x => x.ServiceRadiusKm.HasValue)
            .WithMessage("نطاق المسافة يجب أن يكون بين ١ و٢٠٠ كم.");
    }
}

public sealed class SocialLinksRequestValidator : AbstractValidator<SocialLinksRequest>
{
    public SocialLinksRequestValidator()
    {
        RuleFor(x => x.Links).NotNull();
        RuleForEach(x => x.Links).ChildRules(link =>
        {
            link.RuleFor(l => l.Platform).Must(SocialPlatforms.All.Contains).WithMessage("منصة التواصل غير صحيحة.");
            link.RuleFor(l => l.Url).NotEmpty().Must(BeHttpUrl).WithMessage("رابط الحساب غير صحيح.");
        });
    }

    private static bool BeHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed class BookInterviewRequestValidator : AbstractValidator<BookInterviewRequest>
{
    public BookInterviewRequestValidator()
    {
        RuleFor(x => x.InterviewSlotId).NotEmpty().WithMessage("الموعد مطلوب.");
    }
}

public sealed class SetAvailabilityRequestValidator : AbstractValidator<SetAvailabilityRequest>
{
    public SetAvailabilityRequestValidator()
    {
        RuleFor(x => x.Slots).NotNull();
        RuleForEach(x => x.Slots).ChildRules(slot =>
        {
            slot.RuleFor(s => s.StartsAt).NotEmpty();
            slot.RuleFor(s => s.EndsAt).GreaterThan(s => s.StartsAt).WithMessage("وقت النهاية يجب أن يكون بعد البداية.");
            slot.RuleFor(s => s.Timezone).NotEmpty().Must(BeKnownTimezone).WithMessage("المنطقة الزمنية غير صحيحة.");
        });
        RuleFor(x => x.Slots).Must(slots => slots.Count <= 50).WithMessage("لا يمكن إضافة أكثر من ٥٠ فترة.");
        RuleFor(x => x.Slots).Custom((slots, context) =>
        {
            if (slots is null)
            {
                return;
            }

            if (slots.Any(s => s.StartsAt <= DateTime.UtcNow))
            {
                context.AddFailure(nameof(SetAvailabilityRequest.Slots), "يجب أن تكون الفترات في المستقبل.");
                return;
            }

            var ordered = slots.OrderBy(s => s.StartsAt).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartsAt < ordered[i - 1].EndsAt)
                {
                    context.AddFailure(nameof(SetAvailabilityRequest.Slots), "لا يمكن أن تتداخل فترات التوفر.");
                    return;
                }
            }
        });
    }

    private static bool BeKnownTimezone(string timezone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }
}

public sealed class SubmitTrainingAttemptRequestValidator : AbstractValidator<SubmitTrainingAttemptRequest>
{
    public SubmitTrainingAttemptRequestValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0, 100).WithMessage("الدرجة يجب أن تكون بين ٠ و١٠٠.");
    }
}

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200).WithMessage("اسم المؤسسة مطلوب.");
        RuleFor(x => x.Field).Must(MetaCatalog.IsValidField).When(x => !string.IsNullOrWhiteSpace(x.Field))
            .WithMessage("مجال العمل غير صحيح.");
        RuleFor(x => x.EmployeeCountRange).Must(MetaCatalog.IsValidEmployeeRange).When(x => !string.IsNullOrWhiteSpace(x.EmployeeCountRange))
            .WithMessage("عدد الموظفين غير صحيح.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Website).MaximumLength(300)
            .Must(v => string.IsNullOrWhiteSpace(v) || (Uri.TryCreate(v, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
            .WithMessage("الموقع الإلكتروني غير صحيح.");
        RuleFor(x => x.ContactInformation).MaximumLength(2000);
        // التحقق من المحافظة/المنطقة انتقل إلى OrganizationService لأنه يعتمد على
        // الكتالوج الديناميكي في قاعدة البيانات (IGeoCatalog) وليس قائمة ثابتة.
    }
}

public sealed class DecisionRequestValidator : AbstractValidator<DecisionRequest>
{
    public DecisionRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("الملاحظات طويلة جدًا.");
        RuleFor(x => x)
            .Must(x => x.Approve || !string.IsNullOrWhiteSpace(x.Notes))
            .WithMessage("اذكر سبب الرفض في الملاحظات.");
    }
}

public sealed class CreateInterviewSlotRequestValidator : AbstractValidator<CreateInterviewSlotRequest>
{
    public CreateInterviewSlotRequestValidator()
    {
        RuleFor(x => x.StartsAt).NotEmpty().WithMessage("وقت الموعد مطلوب.")
            .GreaterThan(DateTime.UtcNow.AddMinutes(-2))
            .WithMessage("وقت الموعد يجب أن يكون في المستقبل.");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 120).WithMessage("مدة المقابلة يجب أن تكون بين ١٥ و١٢٠ دقيقة.");
    }
}

public sealed class ResolveDeletionRequestRequestValidator : AbstractValidator<ResolveDeletionRequestRequest>
{
    public ResolveDeletionRequestRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
