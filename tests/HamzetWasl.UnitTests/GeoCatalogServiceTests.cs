using HamzetWasl.Application.Metadata;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Batch 3 (S5): the geo catalogue reads GOVERNORATE/AREA, hides inactive rows,
/// and falls back to the static MetaCatalog only while the tables are empty.
/// </summary>
public class GeoCatalogServiceTests
{
    private static HamzetWaslDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HamzetWaslDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static GeoCatalogService NewService(HamzetWaslDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Empty_database_falls_back_to_the_static_catalog()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var governorates = await service.GetGovernoratesAsync();

        Assert.Equal(MetaCatalog.Governorates.Count, governorates.Count);
        Assert.Contains(governorates, g => g.Key == "cairo");

        // The fallback accepts the Arabic name as well as the code, like the DB path.
        var cairo = await service.FindGovernorateAsync("القاهرة");
        Assert.NotNull(cairo);
        Assert.Equal("cairo", cairo!.Key);

        var area = await service.FindAreaAsync(cairo, "nasr_city");
        Assert.NotNull(area);
        Assert.Equal("nasr_city", area!.Key);
    }

    [Fact]
    public async Task Active_rows_are_returned_and_inactive_rows_are_excluded()
    {
        await using var db = NewDb();

        db.Governorates.AddRange(
            new Governorate
            {
                Code = "cairo",
                NameAr = "القاهرة",
                IsActive = true,
                Areas =
                {
                    new Area { Code = "nasr_city", NameAr = "مدينة نصر", IsActive = true },
                    new Area { Code = "hidden", NameAr = "منطقة معطلة", IsActive = false }
                }
            },
            new Governorate
            {
                Code = "disabled_gov",
                NameAr = "محافظة معطلة",
                IsActive = false
            });

        await db.SaveChangesAsync();

        var service = NewService(db);
        var governorates = await service.GetGovernoratesAsync();

        var governorate = Assert.Single(governorates);
        Assert.Equal("cairo", governorate.Key);
        Assert.Equal("القاهرة", governorate.NameAr);

        var area = Assert.Single(governorate.Areas);
        Assert.Equal("nasr_city", area.Key);

        Assert.Null(await service.FindGovernorateAsync("disabled_gov"));
        Assert.Null(await service.FindAreaAsync(governorate, "hidden"));
    }
}
