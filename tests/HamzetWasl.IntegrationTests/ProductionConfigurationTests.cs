using System.Text.Json;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// The production configuration must not expose the API surface and must not write the
/// request URLs (which carry signatures and tokens) to the log file.
/// </summary>
public class ProductionConfigurationTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HamzetWasl.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    [Fact]
    public void The_serilog_configuration_silences_the_request_and_sql_loggers()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "HamzetWasl.Api", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var overrides = document.RootElement
            .GetProperty("Serilog")
            .GetProperty("MinimumLevel")
            .GetProperty("Override");

        // The hosting diagnostics events log the full request URL (with the query string).
        Assert.Equal("Warning", overrides.GetProperty("Microsoft.AspNetCore.Hosting.Diagnostics").GetString());
        Assert.Equal("Warning", overrides.GetProperty("Microsoft.EntityFrameworkCore.Database.Command").GetString());
    }

    [Fact]
    public void The_production_configuration_has_no_credential_values()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "HamzetWasl.Api", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        Assert.Equal(string.Empty, document.RootElement.GetProperty("Jwt").GetProperty("SigningKey").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("Storage").GetProperty("SigningKey").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("Otp").GetProperty("HashKey").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("Seed").GetProperty("AdminPassword").GetString());
    }
}
