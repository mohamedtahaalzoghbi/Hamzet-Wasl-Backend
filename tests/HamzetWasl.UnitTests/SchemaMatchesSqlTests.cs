using System.Text;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.SchemaComparison;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// The database is the SQL script (MASTER_PROMPT §1.1). These tests fail whenever the EF
/// model drifts from Hamzet_Wasl_Schema_v2.sql: table set, columns (name, type,
/// nullability), PK, FK, UNIQUE, CHECK and index definitions.
/// </summary>
public class SchemaMatchesSqlTests
{
    private static HamzetWaslDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<HamzetWaslDbContext>()
            .UseSqlServer("Server=(local);Database=SchemaComparisonOnly;Trusted_Connection=True;")
            .Options;

        return new HamzetWaslDbContext(options);
    }

    [Fact]
    public void Sql_script_defines_exactly_the_62_domain_tables()
    {
        var script = SqlSchemaParser.Parse(ReadSchemaScript());
        Assert.Equal(62, script.TableCount);
    }

    [Fact]
    public void Ef_model_matches_the_sql_schema_exactly()
    {
        var script = SqlSchemaParser.Parse(ReadSchemaScript());
        using var context = NewContext();

        // IDesignTimeModel has the full store model: indexes, unique/PK/FK constraints and checks.
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var tables = designModel.GetRelationalModel().Tables.ToList();

        var issues = SqlSchemaComparer.Compare(script, tables, "AspNet", "__");

        if (issues.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine($"EF model differs from Hamzet_Wasl_Schema_v2.sql in {issues.Count} place(s):");
            foreach (var issue in issues)
            {
                report.AppendLine(" - " + issue);
            }

            // Assert.Fail keeps the readable diff while failing with a clear message.
            Assert.Fail(report.ToString());
        }
    }

    private static string ReadSchemaScript()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var candidate = Path.GetFullPath(Path.Combine(root, "Hamzet_Wasl_Schema_v2.sql"));
        Assert.True(File.Exists(candidate), $"Authoritative schema not found at {candidate}.");
        return File.ReadAllText(candidate);
    }
}
