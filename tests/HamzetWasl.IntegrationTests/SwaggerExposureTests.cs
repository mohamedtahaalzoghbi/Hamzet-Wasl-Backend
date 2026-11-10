using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Outside development the OpenAPI document must not be served: it maps the whole admin
/// surface for anyone who can reach the host.
/// </summary>
[Collection("Api")]
public class SwaggerExposureTests(ApiFixture fixture)
{
    [Fact]
    public async Task The_openapi_document_is_not_served_outside_development()
    {
        // Outside development the appsettings.Development.json is not loaded, so the keys
        // are provided through the environment for the lifetime of this host.
        var keys = new Dictionary<string, string?>
        {
            ["Jwt__SigningKey"] = "production-like-signing-key-that-is-long-enough-0123456789",
            ["Storage__SigningKey"] = "production-like-storage-key-that-is-long-enough-0123456789",
            ["Otp__HashKey"] = "production-like-otp-key-that-is-long-enough-0123456789"
        };

        var previous = keys.Keys.ToDictionary(key => key, key => Environment.GetEnvironmentVariable(key));
        foreach (var (key, value) in keys)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            using var factory = new HamzetWaslWebApplicationFactory(
                fixture.ConnectionString,
                new Dictionary<string, string?> { ["Swagger:Enabled"] = "false" },
                environment: "Production");

            using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost")
            });

            var document = await client.GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, document.StatusCode);

            var ui = await client.GetAsync("/swagger/index.html");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, ui.StatusCode);
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
