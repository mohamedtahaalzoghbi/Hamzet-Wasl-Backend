using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HamzetWasl.Infrastructure.Persistence;

/// <summary>
/// Creates the database (when missing) and applies the authoritative
/// <c>Hamzet_Wasl_Schema_v2.sql</c> script. The script is the single source of truth
/// for the schema; EF Core migrations are not used.
/// </summary>
public sealed class SchemaBootstrapper(ILogger<SchemaBootstrapper> logger)
{
    private const string ScriptResourceName = "Hamzet_Wasl_Schema_v2.sql";
    private const string AnchorTable = "USER_ACCOUNT";

    /// <summary>
    /// Ensures the target database exists and contains the domain schema.
    /// The script is applied only when the domain tables are missing, so restarts are cheap.
    /// Returns true when the script was applied by this call.
    /// </summary>
    public async Task<bool> EnsureAsync(string connectionString, CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(connectionString, ct);

        if (await DomainSchemaExistsAsync(connectionString, ct))
        {
            logger.LogInformation("Database schema is already present; the SQL script was not re-applied.");
            return false;
        }

        var script = await ReadScriptAsync(ct);
        logger.LogInformation("Applying {Script} to the database…", ScriptResourceName);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var batch in SplitBatches(script))
        {
            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("The authoritative schema was applied successfully.");
        return true;
    }

    private async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "The connection string must contain a database name (Initial Catalog).");
        }

        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var check = new SqlCommand(
            "SELECT CASE WHEN DB_ID(@name) IS NULL THEN 0 ELSE 1 END", connection);
        check.Parameters.AddWithValue("@name", database);
        var exists = await check.ExecuteScalarAsync(ct) is int value && value == 1;

        if (exists)
        {
            return;
        }

        logger.LogInformation("Creating database {Database}…", database);
        await using var create = new SqlCommand($"CREATE DATABASE [{database}]", connection);
        await create.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> DomainSchemaExistsAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'dbo." + AnchorTable + "', N'U') IS NULL THEN 0 ELSE 1 END", connection);

        return await command.ExecuteScalarAsync(ct) is int value && value == 1;
    }

    private static async Task<string> ReadScriptAsync(CancellationToken ct)
    {
        var assembly = typeof(SchemaBootstrapper).GetTypeInfo().Assembly;
        await using var stream = assembly.GetManifestResourceStream(ScriptResourceName)
            ?? throw new InvalidOperationException(
                $"The embedded resource {ScriptResourceName} was not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    /// <summary>Splits the script on standalone "GO" batch separators.</summary>
    private static IEnumerable<string> SplitBatches(string script)
    {
        var builder = new StringBuilder();
        foreach (var line in script.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r', ' ', '\t');
            if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (builder.ToString().Trim().Length > 0)
                {
                    yield return builder.ToString();
                }

                builder.Clear();
                continue;
            }

            builder.AppendLine(line);
        }

        if (builder.ToString().Trim().Length > 0)
        {
            yield return builder.ToString();
        }
    }
}
