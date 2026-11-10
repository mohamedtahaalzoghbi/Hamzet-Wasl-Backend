using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Boots the real API against the test database. Database initialisation is
/// skipped because the fixture already applied the authoritative schema.
/// </summary>
public sealed class HamzetWaslWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public HamzetWaslWebApplicationFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["SkipDatabaseInitialization"] = "true"
            };

            configuration.AddInMemoryCollection(overrides);
        });
    }
}
