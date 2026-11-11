using HamzetWasl.Application.Contracts.Meta;

namespace HamzetWasl.Application.Interfaces;

/// <summary>
/// الكتالوج الجغرافي الديناميكي (المحافظات والمناطق).
///
/// يقرأ الصفوف النشطة فقط من <c>GOVERNORATE</c>/<c>AREA</c>، ويستخدم ذاكرة مؤقتة قصيرة،
/// ويعود إلى <c>MetaCatalog</c> الثابت فقط عندما تكون الجداول فارغة تماماً (قبل التعبئة).
/// </summary>
public interface IGeoCatalog
{
    Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default);

    /// <summary>يقبل الكود (<c>cairo</c>) أو الاسم العربي (<c>القاهرة</c>).</summary>
    Task<GovernorateDto?> FindGovernorateAsync(string? keyOrName, CancellationToken ct = default);

    /// <summary>يقبل كود المنطقة أو اسمها العربي داخل المحافظة المحددة.</summary>
    Task<AreaDto?> FindAreaAsync(GovernorateDto governorate, string? keyOrName, CancellationToken ct = default);
}
