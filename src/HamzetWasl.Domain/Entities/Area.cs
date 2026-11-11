using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

/// <summary>
/// منطقة داخل محافظة. الكود فريد داخل المحافظة الواحدة (وليس عالمياً)، ويُستخدم
/// <see cref="IsActive"/> لإخفائها من الـ API دون حذف الصف.
/// </summary>
public partial class Area
{
    public int AreaId { get; set; }

    public int GovernorateId { get; set; }

    /// <summary>المعرّف النصي المستقر (slug) مثل <c>nasr_city</c>.</summary>
    public string Code { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public virtual Governorate Governorate { get; set; } = null!;
}
