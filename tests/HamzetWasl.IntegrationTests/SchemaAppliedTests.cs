using System.Text.RegularExpressions;
using HamzetWasl.Infrastructure.SchemaComparison;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Proves that the authoritative SQL script is applied to the real database exactly:
/// the 60 domain tables, their CHECK constraints, foreign keys, unique constraints
/// and indexes come from <c>Hamzet_Wasl_Schema_v2.sql</c> and nothing else.
/// </summary>
[Collection("Api")]
public class SchemaAppliedTests(ApiFixture fixture)
{
    [Fact]
    public async Task Database_contains_exactly_the_tables_of_the_sql_script()
    {
        var script = SqlSchemaParser.Parse(ReadSchemaScript());

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var databaseTables = await QueryAsync(connection,
            "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo')",
            reader => reader.GetString(0));

        var domainTables = databaseTables
            .Where(name => !name.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expected = script.Tables.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(expected, domainTables);
        Assert.Equal(60, domainTables.Count);
    }

    [Fact]
    public async Task Database_check_constraints_match_the_sql_script()
    {
        var script = SqlSchemaParser.Parse(ReadSchemaScript());

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var databaseChecks = await QueryAsync(connection,
            """
            SELECT t.name, cc.name, cc.definition
            FROM sys.check_constraints cc
            INNER JOIN sys.tables t ON t.object_id = cc.parent_object_id
            WHERE SCHEMA_NAME(t.schema_id) = 'dbo'
            """,
            reader => (Table: reader.GetString(0), Name: reader.GetString(1), Definition: reader.GetString(2)));

        var domainChecks = databaseChecks
            .Where(c => !c.Table.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var expected = script.Tables.Values
            .SelectMany(t => t.Checks.Select(c => (t.Name, c.Name, c.Definition)))
            .ToList();

        Assert.Equal(expected.Count, domainChecks.Count);

        foreach (var (table, name, definition) in expected)
        {
            var match = domainChecks.FirstOrDefault(c =>
                c.Table.Equals(table, StringComparison.OrdinalIgnoreCase) &&
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            Assert.True(match != default, $"CHECK {name} is missing on table {table}.");

            // SQL Server rewrites `col IN (N'a', N'b')` into `([col]=N'a' OR [col]=N'b')`,
            // so the definitions are compared by the values they allow.
            Assert.Equal(
                ExtractLiterals(definition).OrderBy(v => v, StringComparer.Ordinal),
                ExtractLiterals(match.Definition).OrderBy(v => v, StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task Database_foreign_keys_unique_constraints_and_indexes_match_the_sql_script()
    {
        var script = SqlSchemaParser.Parse(ReadSchemaScript());

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var foreignKeys = await QueryAsync(connection,
            """
            SELECT fk.name
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t ON t.object_id = fk.parent_object_id
            WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name NOT LIKE 'AspNet%'
            """,
            reader => reader.GetString(0));

        var expectedForeignKeys = script.Tables.Values.SelectMany(t => t.ForeignKeys).Select(fk => fk.Name).ToList();
        Assert.Equal(expectedForeignKeys.Count, foreignKeys.Count);
        foreach (var expected in expectedForeignKeys)
        {
            Assert.Contains(foreignKeys, actual => actual.Equals(expected, StringComparison.OrdinalIgnoreCase));
        }

        var uniqueConstraints = await QueryAsync(connection,
            """
            SELECT uq.name
            FROM sys.key_constraints uq
            INNER JOIN sys.tables t ON t.object_id = uq.parent_object_id
            WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name NOT LIKE 'AspNet%' AND uq.type = 'UQ'
            """,
            reader => reader.GetString(0));

        var uniqueIndexes = await QueryAsync(connection,
            """
            SELECT i.name
            FROM sys.indexes i
            INNER JOIN sys.tables t ON t.object_id = i.object_id
            WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name NOT LIKE 'AspNet%'
              AND i.is_unique = 1 AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
            """,
            reader => reader.GetString(0));

        var expectedUniques = script.Tables.Values.SelectMany(t => t.Uniques).Select(u => u.Name).ToList();
        var actualUniques = uniqueConstraints.Concat(uniqueIndexes).ToList();

        Assert.Equal(expectedUniques.Count, actualUniques.Count);
        foreach (var expected in expectedUniques)
        {
            Assert.Contains(actualUniques, actual =>
                actual.Equals(expected, StringComparison.OrdinalIgnoreCase));
        }

        var indexes = await QueryAsync(connection,
            """
            SELECT i.name
            FROM sys.indexes i
            INNER JOIN sys.tables t ON t.object_id = i.object_id
            WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name NOT LIKE 'AspNet%'
              AND i.is_unique = 0 AND i.name IS NOT NULL AND i.is_primary_key = 0
            """,
            reader => reader.GetString(0));

        var expectedIndexes = script.Tables.Values.SelectMany(t => t.Indexes).Select(i => i.Name).ToList();
        Assert.Equal(expectedIndexes.Count, indexes.Count);
        foreach (var expected in expectedIndexes)
        {
            Assert.Contains(indexes, actual => actual.Equals(expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string Canonicalise(string value) =>
        Regex.Replace((value ?? string.Empty).Replace('\'', '"').ToLowerInvariant(), @"\s+", string.Empty);

    /// <summary>All string literals inside a CHECK definition (the values the check allows).</summary>
    private static IReadOnlyList<string> ExtractLiterals(string definition) =>
        Regex.Matches(definition ?? string.Empty, @"N?'([^']*)'", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Where(value => value.Length > 0)
            .ToList();

    private static async Task<List<T>> QueryAsync<T>(
        SqlConnection connection, string sql, Func<SqlDataReader, T> map)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<T>();
        while (await reader.ReadAsync())
        {
            results.Add(map(reader));
        }

        return results;
    }

    private static string ReadSchemaScript()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var candidate = Path.GetFullPath(Path.Combine(root, "Hamzet_Wasl_Schema_v2.sql"));
        Assert.True(File.Exists(candidate), $"Authoritative schema not found at {candidate}.");
        return File.ReadAllText(candidate);
    }
}
