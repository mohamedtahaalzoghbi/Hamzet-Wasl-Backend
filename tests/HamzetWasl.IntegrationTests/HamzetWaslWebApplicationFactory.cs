using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Boots the real API against the test database. Database initialisation is
/// skipped because the fixture already applied the authoritative schema.
/// </summary>
public sealed class HamzetWaslWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly Dictionary<string, string?> _extraConfiguration;
    private readonly string _environment;

    public HamzetWaslWebApplicationFactory(
        string connectionString,
        Dictionary<string, string?>? extraConfiguration = null,
        string environment = "Development")
    {
        _connectionString = connectionString;
        _extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
        _environment = environment;
    }

    /// <summary>Captures the OTP messages so tests can verify email and phone.</summary>
    public CapturingMessageSender Messages { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["SkipDatabaseInitialization"] = "true",

                // The suite performs many authentication calls from one address; the limits
                // are raised so the functional tests are not throttled. The limiter itself is
                // covered by RateLimitingTests with its own host.
                ["RateLimiting:AuthPermitLimit"] = "100000",
                ["RateLimiting:OtpPermitLimit"] = "10000",
                ["RateLimiting:UploadPermitLimit"] = "10000"
            };

            foreach (var entry in _extraConfiguration)
            {
                overrides[entry.Key] = entry.Value;
            }

            configuration.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<IEmailSender>(Messages);
            services.AddSingleton<ISmsSender>(Messages);

            // Keep the real SignalR publisher but record what is published.
            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<HamzetWasl.Api.Realtime.SignalRNotificationPublisher>();
            services.AddSingleton(sp => new RecordingNotificationPublisher(
                sp.GetRequiredService<HamzetWasl.Api.Realtime.SignalRNotificationPublisher>()));
            services.AddSingleton<INotificationPublisher>(sp =>
                sp.GetRequiredService<RecordingNotificationPublisher>());
        });
    }
}
