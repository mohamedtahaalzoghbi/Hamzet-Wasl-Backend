using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Shops;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class ShopsFlowTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;
    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";
    private string Phone()
    {
        var ticks = DateTime.UtcNow.Ticks % 100_000_000;
        return $"010{ticks:D8}";
    }

    /// <summary>Registers a user through the two-step OTP flow and asserts it succeeded.</summary>
    private async Task<AuthResponse> Register(string role, string name, string email, string phone)
    {
        return await Client.RegisterVerifiedAsync(fixture.Messages, RegisterBody(role, name, email, phone));
    }

    /// <summary>Registers a user and asserts the call failed with the given status.</summary>
    private async Task<ApiError> RegisterFailing(
        string role, string name, string email, string phone, int expectedStatus)
    {
        var (status, _, error) =
            await Client.PostAsync<AuthResponse>("/api/v1/auth/register", RegisterBody(role, name, email, phone));
        Assert.Equal(expectedStatus, status);
        Assert.NotNull(error);
        return error!;
    }

    private static object RegisterBody(string role, string name, string email, string phone) => new
    {
        role,
        name = role is "association" or "company" or "shop" ? null : name,
        organizationName = role is "association" or "company" or "shop" ? name : null,
        email,
        phone,
        password = "Passw0rd!",
        confirmPassword = "Passw0rd!",
        termsAccepted = true,
        policyVersion = "1.0"
    };

    private async Task<AuthResponse> AdminLogin()
    {
        var (status, data, _) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = "admin@hamzetwasl.local",
            password = "Admin@12345"
        });

        Assert.Equal(200, status);
        Assert.NotNull(data);
        return data!;
    }


    private async Task<HttpClient> ApprovedShop(HttpClient admin, string name)
    {
        var shop = Authed((await Register("shop", name, Email("sh"), Phone())).AccessToken);
        await shop.PutAsync<OrganizationResponse>("/api/v1/me/organization", new { name, field = "technology", description = "محل تجربة", governorate = "cairo", city = "nasr_city", website = "https://example.com", employeeCountRange = "11-50", contactInformation = "0100000000" });
        await shop.UploadAsync<OrganizationResponse>("/api/v1/me/organization/logo", "logo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png");
        var pdf = "%PDF-1.4\n%Test document\n%%EOF"u8.ToArray();
        foreach (var t in new[] { "commercial_register", "tax_card", "address_proof" })
            await shop.UploadAsync<OrganizationResponse>($"/api/v1/me/organization/documents/{t}", $"{t}.pdf", pdf, "application/pdf");
        var (_, sub, _) = await shop.PostAsync<SubmitOrganizationResponse>("/api/v1/me/organization/submit");
        await admin.PostAsync<object?>($"/api/v1/admin/organization-reviews/{sub!.ReviewId}/decision", new { approve = true, notes = (string?)null });
        return shop;
    }

    [Fact]
    public async Task Shops_lifecycle()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var name = "Shop " + Suffix;
        var shop = Authed((await Register("shop", name, Email("sx"), Phone())).AccessToken);
        var du = Authed((await Register("disabled_user", "Shopper", Email("sd"), Phone())).AccessToken);

        // Not approved yet: cannot add products.
        var (sPre, _, _) = await shop.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "x", description = "d", price = 5, available = true });
        Assert.Equal(403, sPre);

        var approved = await ApprovedShop(admin, "Approved " + Suffix);
        var (sBad, _, _) = await approved.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "", description = "d", price = 5, available = true });
        Assert.Equal(400, sBad);
        var (sNeg, _, _) = await approved.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "x", price = -1, available = true });
        Assert.Equal(400, sNeg);
        var (sUser, _, _) = await du.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "x", price = 1, available = true });
        Assert.Equal(403, sUser);

        var (_, p1, e1) = await approved.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "كرسي متحرك", description = "خفيف", price = 3500, available = true });
        Assert.Null(e1);
        var (_, p2, _) = await approved.PostAsync<ProductDto>("/api/v1/shops/products", new { name = "عصا", price = (decimal?)null, available = false });
        var (_, mine, _) = await approved.GetAsync<List<ProductDto>>("/api/v1/shops/mine/products");
        Assert.Equal(2, mine!.Count);

        var (sList, list, _) = await Client.GetAsync<PagedResult<ShopSummaryDto>>("/api/v1/shops");
        Assert.Equal(200, sList);
        var summary = list!.Items.Single(s => s.Name == "Approved " + Suffix);
        Assert.Equal(1, summary.ProductsCount);
        Assert.DoesNotContain(list.Items, s => s.Name == name);

        var (_, detail, _) = await Client.GetAsync<ShopDetailDto>($"/api/v1/shops/{summary.OrganizationId}");
        Assert.Single(detail!.Products);
        Assert.Equal("0100000000", detail.ContactInformation);

        var (_, upd, eu) = await approved.PutAsync<ProductDto>($"/api/v1/shops/products/{p2!.ProductId}", new { name = "عصا", price = 90, available = true });
        Assert.Null(eu);
        Assert.True(upd!.Available);

        var other = await ApprovedShop(admin, "Other " + Suffix);
        var (sForeign, _, _) = await other.PutAsync<ProductDto>($"/api/v1/shops/products/{p1!.ProductId}", new { name = "x", price = 1, available = true });
        Assert.Equal(404, sForeign);

        var (sDel, _, _) = await approved.DeleteAsync<object?>($"/api/v1/shops/products/{p1.ProductId}");
        Assert.Equal(204, sDel);

        // Shops cannot post jobs.
        var (sJob, _, _) = await approved.PostAsync<object?>("/api/v1/jobs", new { title = "x", description = "d", employmentType = "full_time", workArrangement = "remote", location = "x", accessibilityInformation = "x" });
        Assert.Equal(403, sJob);
    }
}
