using HamzetWasl.Domain.Constants;

namespace HamzetWasl.Application.Common;

/// <summary>
/// Arabic titles for the notifications the API sends. The title is derived from the
/// notification type so the stored row and the pushed payload always agree.
/// </summary>
public static class NotificationTitles
{
    /// <summary>Returns the Arabic title of a notification type, or a generic fallback.</summary>
    public static string For(string notificationType) => notificationType switch
    {
        NotificationTypes.OrganizationApproved => "تم اعتماد مؤسستك",
        NotificationTypes.OrganizationRejected => "تم رفض طلب المؤسسة",
        NotificationTypes.VerificationApproved => "تم اعتماد التحقق",
        NotificationTypes.VerificationRejected => "تم رفض طلب التحقق",
        NotificationTypes.InterviewBooked => "تم حجز موعد المقابلة",
        NotificationTypes.InterviewChanged => "تم تغيير موعد المقابلة",
        NotificationTypes.InterviewCancelled => "تم إلغاء موعد المقابلة",
        NotificationTypes.AccountSuspended => "تم إيقاف الحساب",
        NotificationTypes.AccountReactivated => "تمت إعادة تفعيل الحساب",
        _ => "إشعار جديد"
    };
}
