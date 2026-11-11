using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

/// <summary>
/// محافظة في الكتالوج الجغرافي الديناميكي. تُقرأ من جدول <c>GOVERNORATE</c> بدلاً من
/// القائمة الثابتة في <c>MetaCatalog</c>، ويمكن تعطيلها بـ <see cref="IsActive"/> دون حذفها.
/// </summary>
public partial class Governorate
{
    public int GovernorateId { get; set; }

    /// <summary>المعرّف النصي المستقر (slug) مثل <c>cairo</c>؛ هو ما يُخزَّن في الملفات الشخصية.</summary>
    public string Code { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}
