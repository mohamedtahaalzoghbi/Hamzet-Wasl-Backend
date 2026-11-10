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
    private const string LocalDbInstance = "(localdb)\\MSSQLLocalDB";
    private string _databaseName = default!;
    private string _connectionString = default!;
    private string _masterConnectionString = default!;

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;
    public HttpClient AnonymousClient { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _databaseName = $"HamzetWasl_IT_{Guid.NewGuid():N}"[..40];
        _masterConnectionString = $"Server={LocalDbInstance};Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        _connectionString = $"Server={LocalDbInstance};Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

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

        Factory = new HamzetWaslWebApplicationFactory(_connectionString);
        AnonymousClient = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();
        await IdentitySchemaInitializer.InitializeAsync(db);
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
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
