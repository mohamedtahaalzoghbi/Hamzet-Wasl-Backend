using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class RenderConfigurationTests(ApiFixture fixture)
{
    [Fact]
    public async Task Production_swagger_is_enabled_by_default_for_the_demo()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task AllowAll_preflight_accepts_authorization_without_cookie_credentials()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://flutter.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task AllowAll_can_be_disabled_to_restore_the_origin_allowlist()
    {
        using var factory = NewFactory(new() { ["Cors:AllowAll"] = "false" });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://not-allowed.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Render_http_upstream_does_not_redirect_when_flag_is_disabled()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private HamzetWaslWebApplicationFactory NewFactory(Dictionary<string, string?>? extra = null)
    {
        var settings = extra ?? new();
        settings["HttpsRedirection:Enabled"] = "false";
        return new HamzetWaslWebApplicationFactory(fixture.ConnectionString, settings, "Production");
    }
}
