using System.Text;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Creates a fresh LocalDB database per test run from the authoritative
/// Hamzet_Wasl_Schema_v2.sql file, then boots the API against it.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>
    /// Override for CI (and any non-LocalDB host): HW_TEST_SQL_SERVER, HW_TEST_SQL_USER,
    /// HW_TEST_SQL_PASSWORD. Locally the tests use LocalDB with a trusted connection.
    /// </summary>
    private const string ServerVariable = "HW_TEST_SQL_SERVER";

    private const string UserVariable = "HW_TEST_SQL_USER";
    private const string PasswordVariable = "HW_TEST_SQL_PASSWORD";

    // The signing keys and the seeded admin come from the environment, never from a local
    // appsettings.Development.json (the repository does not commit one). They must be
    // environment variables because the host reads appsettings.json *after* the in-memory
    // configuration of the test host, and that file carries those keys empty.
    private const string TestSigningKey = "integration-test-signing-key-that-is-long-enough-0123456789";

    private const string TestStorageKey = "integration-test-storage-key-that-is-long-enough-0123456789";

    private const string TestOtpKey = "integration-test-otp-key-that-is-long-enough-0123456789";

    private string _databaseName = default!;
    private string _connectionString = default!;
    private string _masterConnectionString = default!;

    public HamzetWaslWebApplicationFactory Factory { get; private set; } = default!;
    public HttpClient AnonymousClient { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _databaseName = $"HamzetWasl_IT_{Guid.NewGuid():N}"[..40];
        _masterConnectionString = BuildConnectionString("master");
        _connectionString = BuildConnectionString(_databaseName);

        await CreateDatabaseAsync();

        var sql = await File.ReadAllTextAsync(SchemaPath);
        var batches = SplitGoBatches(sql).ToArray();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = new SqlCommand(batch, connection);
            command.ExecuteNonQuery();
        }

        // The API reads its connection string while the host is built, so it is provided
        // through the environment (the ASP.NET Core `ConnectionStrings__DefaultConnection` key).
        // A late `ConfigureAppConfiguration` override would arrive after the DbContext was
        // already registered with the LocalDB default.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);

        // Same rule for the signing keys and the seeded admin: the JSON configuration files
        // are applied after the in-memory collection of the test host, so these values must
        // arrive through the environment. That is what makes the suite independent from any
        // local configuration file.
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Storage__SigningKey", TestStorageKey);
        Environment.SetEnvironmentVariable("Otp__HashKey", TestOtpKey);
        Environment.SetEnvironmentVariable("Seed__AdminEmail", "admin@hamzetwasl.local");
        Environment.SetEnvironmentVariable("Seed__AdminPhone", "01000000000");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", "Admin@12345");

        Factory = new HamzetWaslWebApplicationFactory(_connectionString);
        AnonymousClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();
        await IdentitySchemaInitializer.InitializeAsync(db);
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    /// <summary>The connection string of the test database (exposed for catalog assertions).</summary>
    public string ConnectionString => _connectionString;

    /// <summary>The captured OTP messages (email and SMS).</summary>
    public CapturingMessageSender Messages => Factory.Messages;

    /// <summary>Creates a client bound to the in-memory test server.</summary>
    public HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost")
    });

    private static string BuildConnectionString(string database)
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            return $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;" +
                   "TrustServerCertificate=True;MultipleActiveResultSets=True";
        }

        var user = Environment.GetEnvironmentVariable(UserVariable) ?? "sa";
        var password = Environment.GetEnvironmentVariable(PasswordVariable) ?? "Password123!";
        return $"Server={server};Database={database};User Id={user};Password={password};" +
               "TrustServerCertificate=True;MultipleActiveResultSets=True";
    }

    public Task DisposeAsync()
    {
        AnonymousClient.Dispose();
        Factory.Dispose();

        try
        {
            using var connection = new SqlConnection(_masterConnectionString);
            connection.OpenAsync().GetAwaiter().GetResult();
            var clear = $"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{_databaseName}') ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
            using (var command = new SqlCommand(clear, connection))
            {
                command.ExecuteNonQuery();
            }

            var drop = $"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{_databaseName}') DROP DATABASE [{_databaseName}]";
            using var dropCommand = new SqlCommand(drop, connection);
            dropCommand.ExecuteNonQuery();
        }
        catch (SqlException)
        {
            // LocalDB may still be tearing down. The next run uses a fresh database name.
        }

        return Task.CompletedTask;
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{_databaseName}') CREATE DATABASE [{_databaseName}]",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string SchemaPath
    {
        get
        {
            var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
            var candidate = Path.GetFullPath(Path.Combine(root, "Hamzet_Wasl_Schema_v2.sql"));
            Assert.True(File.Exists(candidate), $"Authoritative schema not found at {candidate}.");
            return candidate;
        }
    }

    /// <summary>Splits a SQL script on standalone "GO" batch separators.</summary>
    private static IEnumerable<string> SplitGoBatches(string sql)
    {
        var builder = new StringBuilder();
        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r', ' ', '\t');
            if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return builder.ToString();
                builder.Clear();
            }
            else
            {
                builder.AppendLine(line);
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}

/// <summary>Shared fixture lifetime across test classes (xUnit collection fixture).</summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
}
