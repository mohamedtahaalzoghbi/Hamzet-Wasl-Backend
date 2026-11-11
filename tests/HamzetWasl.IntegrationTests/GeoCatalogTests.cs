using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Batch 3 (S5): the governorate/area catalogue is read from GOVERNORATE/AREA, the meta and
/// profile endpoints expose it, and the service-level validation replaces the old static
/// FluentValidation rules (which now accept any string, see ValidatorTests).
/// </summary>
[Collection("Api")]
public class GeoCatalogTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private int _registrationCounter;

    private string Email(string prefix) => $"{prefix}{Suffix}{_registrationCounter++}@example.com";

    private static string Phone() => "010" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync(string role, string name)
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/register", new
        {
            role,
            name = role is "association" or "company" ? null : name,
            organizationName = role is "association" or "company" ? name : null,
            email = Email(role),
            phone = Phone(),
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
            termsAccepted = true,
            policyVersion = "1.0"
        });

        Assert.Equal(201, status);
        Assert.Null(error);
        return data!;
    }

    private static bool HasField(ApiError error, string field) =>
        error.Fields is not null &&
        error.Fields.Keys.Any(key => string.Equals(key, field, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------- meta

    [Fact]
    public async Task Meta_returns_the_database_governorates_and_areas()
    {
        var (status, governorates, error) =
            await Client.GetAsync<IReadOnlyList<GovernorateDto>>("/api/v1/meta/governorates");

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(governorates);

        var cairo = Assert.Single(governorates!, g => g.Key == "cairo");
        Assert.Equal("القاهرة", cairo.NameAr);
        Assert.Contains(cairo.Areas, a => a.Key == "nasr_city");

        var (areasStatus, areas, areasError) =
            await Client.GetAsync<IReadOnlyList<AreaDto>>("/api/v1/meta/governorates/cairo/areas");

        Assert.Equal(200, areasStatus);
        Assert.Null(areasError);
        Assert.Contains(areas!, a => a.Key == "nasr_city");

        var (missingStatus, _, missingError) =
            await Client.GetAsync<IReadOnlyList<AreaDto>>("/api/v1/meta/governorates/atlantis/areas");

        Assert.Equal(404, missingStatus);
        Assert.NotNull(missingError);
        Assert.Equal(ErrorCodes.NotFound, missingError!.Code);
    }

    [Fact]
    public async Task Inactive_geo_rows_are_hidden_from_the_meta_endpoints()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();
            db.Governorates.Add(new Governorate
            {
                Code = "test_gov",
                NameAr = "محافظة اختبار",
                IsActive = true,
                Areas =
                {
                    new Area { Code = "test_area_active", NameAr = "منطقة نشطة", IsActive = true },
                    new Area { Code = "test_area_hidden", NameAr = "منطقة معطلة", IsActive = false }
                }
            });
            db.Governorates.Add(new Governorate
            {
                Code = "hidden_gov",
                NameAr = "محافظة معطلة",
                IsActive = false
            });
            await db.SaveChangesAsync();
        }

        // The service caches for ten minutes; a direct insert is not visible until it is cleared.
        fixture.Factory.Services.GetRequiredService<IMemoryCache>().Remove(GeoCatalogService.CacheKey);

        var (status, governorates, error) =
            await Client.GetAsync<IReadOnlyList<GovernorateDto>>("/api/v1/meta/governorates");

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(governorates);

        Assert.Contains(governorates!, g => g.Key == "test_gov");
        Assert.DoesNotContain(governorates!, g => g.Key == "hidden_gov");

        var testGovernorate = Assert.Single(governorates!, g => g.Key == "test_gov");
        var area = Assert.Single(testGovernorate.Areas);
        Assert.Equal("test_area_active", area.Key);
    }

    // --------------------------------------------------------- volunteer

    [Fact]
    public async Task Volunteer_location_uses_the_dynamic_catalogue()
    {
        var user = await RegisterAsync("volunteer", "Geo Volunteer");
        var client = Authed(user.AccessToken);

        var (okStatus, ok, okError) = await client.PutAsync<VolunteerLocationResponse>(
            "/api/v1/me/volunteer-location", new
            {
                governorate = "cairo",
                serviceArea = "nasr_city",
                serviceRadiusKm = 10
            });

        Assert.Equal(200, okStatus);
        Assert.Null(okError);
        Assert.Equal("cairo", ok!.Governorate);
        Assert.Equal("nasr_city", ok.ServiceArea);

        var (badGovernorateStatus, _, badGovernorateError) = await client.PutAsync<VolunteerLocationResponse>(
            "/api/v1/me/volunteer-location", new
            {
                governorate = "paris",
                serviceArea = "nasr_city",
                serviceRadiusKm = 10
            });

        Assert.Equal(400, badGovernorateStatus);
        Assert.NotNull(badGovernorateError);
        Assert.True(HasField(badGovernorateError!, "governorate"));

        var (badAreaStatus, _, badAreaError) = await client.PutAsync<VolunteerLocationResponse>(
            "/api/v1/me/volunteer-location", new
            {
                governorate = "cairo",
                serviceArea = "unknown_area",
                serviceRadiusKm = 10
            });

        Assert.Equal(400, badAreaStatus);
        Assert.NotNull(badAreaError);
        Assert.True(HasField(badAreaError!, "serviceArea"));

        // Arabic names are accepted and normalised to the stable code.
        var (nameStatus, named, nameError) = await client.PutAsync<VolunteerLocationResponse>(
            "/api/v1/me/volunteer-location", new
            {
                governorate = "القاهرة",
                serviceArea = "مدينة نصر",
                serviceRadiusKm = 10
            });

        Assert.Equal(200, nameStatus);
        Assert.Null(nameError);
        Assert.Equal("cairo", named!.Governorate);
        Assert.Equal("nasr_city", named.ServiceArea);
    }

    // ------------------------------------------------------ organization

    [Fact]
    public async Task Organization_update_uses_the_dynamic_catalogue()
    {
        var user = await RegisterAsync("company", "Geo Company");
        var client = Authed(user.AccessToken);

        var (okStatus, ok, okError) = await client.PutAsync<OrganizationResponse>(
            "/api/v1/me/organization", new
            {
                name = "Geo Company",
                field = "technology",
                governorate = "cairo",
                city = "nasr_city",
                website = "https://geo.example.com",
                employeeCountRange = "11-50",
                contactInformation = "geo@example.com"
            });

        Assert.Equal(200, okStatus);
        Assert.Null(okError);
        Assert.Equal("cairo", ok!.Governorate);
        Assert.Equal("nasr_city", ok.City);

        var (badGovernorateStatus, _, badGovernorateError) = await client.PutAsync<OrganizationResponse>(
            "/api/v1/me/organization", new
            {
                name = "Geo Company",
                governorate = "paris",
                city = "nasr_city"
            });

        Assert.Equal(400, badGovernorateStatus);
        Assert.NotNull(badGovernorateError);
        Assert.True(HasField(badGovernorateError!, "governorate"));

        var (badCityStatus, _, badCityError) = await client.PutAsync<OrganizationResponse>(
            "/api/v1/me/organization", new
            {
                name = "Geo Company",
                governorate = "cairo",
                city = "unknown_area"
            });

        Assert.Equal(400, badCityStatus);
        Assert.NotNull(badCityError);
        Assert.True(HasField(badCityError!, "city"));

        // Arabic names are accepted and normalised to the stable code.
        var (nameStatus, named, nameError) = await client.PutAsync<OrganizationResponse>(
            "/api/v1/me/organization", new
            {
                name = "Geo Company",
                governorate = "الجيزة",
                city = "الدقي"
            });

        Assert.Equal(200, nameStatus);
        Assert.Null(nameError);
        Assert.Equal("giza", named!.Governorate);
        Assert.Equal("dokki", named.City);
    }
}
