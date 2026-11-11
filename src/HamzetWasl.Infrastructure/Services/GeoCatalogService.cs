using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Application.Metadata;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// يقرأ المحافظات/المناطق النشطة من قاعدة البيانات مع ذاكرة مؤقتة،
/// ويعود إلى <c>MetaCatalog</c> فقط عندما تكون الجداول فارغة.
/// </summary>
public sealed class GeoCatalogService : IGeoCatalog
{
    /// <summary>مفتاح الذاكرة المؤقتة (يستخدمه الاختبار لإبطالها بعد إدراج صفوف مباشرة).</summary>
    public const string CacheKey = "geo:catalog:v1";

    /// <summary>مدة قصيرة: لا توجد endpoints لإدارة الجغرافيا في Phase 1، فالبيانات شبه ثابتة.</summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly HamzetWaslDbContext _db;
    private readonly IMemoryCache _cache;

    public GeoCatalogService(HamzetWaslDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<GovernorateDto>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _db.Governorates
            .AsNoTracking()
            .Where(g => g.IsActive)
            .Include(g => g.Areas.Where(a => a.IsActive))
            .OrderBy(g => g.GovernorateId)
            .ToListAsync(ct);

        // الجداول موجودة لكنها فارغة تماماً: نستخدم الكتالوج الثابت حتى تعمل الواجهة.
        // لا نُخزّن النتيجة مؤقتاً لأن التعبئة اللاحقة يجب أن تظهر دون إعادة تشغيل.
        if (rows.Count == 0 && !await _db.Governorates.AnyAsync(ct))
        {
            return MetaCatalog.Governorates;
        }

        var result = rows
            .Select(g => new GovernorateDto(
                g.Code,
                g.NameAr,
                g.Areas.OrderBy(a => a.AreaId)
                    .Select(a => new AreaDto(a.Code, a.NameAr))
                    .ToList()))
            .ToList();

        _cache.Set<IReadOnlyList<GovernorateDto>>(CacheKey, result, CacheLifetime);
        return result;
    }

    public async Task<GovernorateDto?> FindGovernorateAsync(string? keyOrName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyOrName))
        {
            return null;
        }

        var governorates = await GetGovernoratesAsync(ct);
        return governorates.FirstOrDefault(g =>
            string.Equals(g.Key, keyOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g.NameAr, keyOrName, StringComparison.Ordinal));
    }

    public Task<AreaDto?> FindAreaAsync(
        GovernorateDto governorate, string? keyOrName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyOrName))
        {
            return Task.FromResult<AreaDto?>(null);
        }

        var area = governorate.Areas.FirstOrDefault(a =>
            string.Equals(a.Key, keyOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.NameAr, keyOrName, StringComparison.Ordinal));

        return Task.FromResult(area);
    }
}
