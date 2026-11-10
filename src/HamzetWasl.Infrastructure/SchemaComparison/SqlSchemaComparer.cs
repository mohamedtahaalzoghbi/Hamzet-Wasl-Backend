using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HamzetWasl.Infrastructure.SchemaComparison;

/// <summary>Compares the parsed SQL schema against the EF store (relational) model.</summary>
public static class SqlSchemaComparer
{
    /// <summary>
    /// Every difference the EF model has with the authoritative schema file.
    /// An empty list means the two definitions are identical.
    /// </summary>
    public static List<string> Compare(
        SqlSchema script,
        IReadOnlyList<ITable> efTables,
        params string[] efOnlyTablePrefixes)
    {
        var issues = new List<string>();

        var efTablesByName = efTables
            .Where(t => string.IsNullOrEmpty(t.Schema) || t.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in script.Tables.Keys)
        {
            if (!efTablesByName.ContainsKey(name))
            {
                issues.Add($"table {name}: declared in SQL but missing from the EF model.");
            }
        }

        foreach (var efName in efTablesByName.Keys)
        {
            if (script.Tables.ContainsKey(efName) ||
                efOnlyTablePrefixes.Any(efName.StartsWith))
            {
                continue;
            }

            issues.Add($"table {efName}: mapped in EF but absent from the SQL schema.");
        }

        foreach (var (name, def) in script.Tables)
        {
            if (!efTablesByName.TryGetValue(name, out var table))
            {
                continue;
            }

            CompareColumns(name, def, table, issues);
            ComparePrimaryKey(name, def, table, issues);
            CompareForeignKeys(name, def, table, issues);
            CompareUniques(name, def, table, issues);
            CompareChecks(name, def, table, issues);
            CompareIndexes(name, def, table, issues);
        }

        return issues;
    }

    private static void CompareColumns(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        var efColumns = ef.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var column in def.Columns)
        {
            if (!efColumns.TryGetValue(column.Name, out var efColumn))
            {
                issues.Add($"{table}.{column.Name}: column missing from the EF model.");
                continue;
            }

            if (!Normalise(column.Type).Equals(Normalise(efColumn.StoreType), StringComparison.Ordinal))
            {
                issues.Add($"{table}.{column.Name}: type '{column.Type}' (sql) vs '{efColumn.StoreType}' (ef).");
            }

            if (column.Nullable != efColumn.IsNullable)
            {
                issues.Add($"{table}.{column.Name}: nullability sql={Spark(column.Nullable)} vs ef={Spark(efColumn.IsNullable)}.");
            }
        }

        foreach (var efColumn in ef.Columns)
        {
            if (def.Columns.All(c => !c.Name.Equals(efColumn.Name, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add($"{table}.{efColumn.Name}: EF declares a column that is not in the SQL schema.");
            }
        }
    }

    private static void ComparePrimaryKey(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        var sqlColumns = def.PrimaryKeyColumns;
        var efColumnNames = ef.PrimaryKey?.Columns.Select(c => c.Name).ToArray() ?? [];

        if (sqlColumns.Count == 0 && efColumnNames.Length == 0)
        {
            return;
        }

        if (efColumnNames.Length == 0)
        {
            issues.Add($"{table}: SQL declares a primary key but EF has none.");
            return;
        }

        if (sqlColumns.Count == 0)
        {
            issues.Add($"{table}: EF declares a primary key but the SQL schema has none.");
            return;
        }

        if (!ColumnsEqual(sqlColumns, efColumnNames))
        {
            issues.Add($"{table}: PK columns sql=({Join(sqlColumns)}) ef=({Join(efColumnNames)}).");
        }
    }

    private static void CompareForeignKeys(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        var efForeignKeys = ef.ForeignKeyConstraints.ToArray();

        foreach (var sqlFk in def.ForeignKeys)
        {
            var match = efForeignKeys.FirstOrDefault(fk => fk.Name.Equals(sqlFk.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                issues.Add($"{table}: FK {sqlFk.Name} missing from the EF model.");
                continue;
            }

            if (!ColumnsEqual(sqlFk.Columns, match.Columns.Select(c => c.Name)))
            {
                issues.Add($"{table}: FK {sqlFk.Name} columns sql=({Join(sqlFk.Columns)}) ef=({Join(match.Columns.Select(c => c.Name))}).");
            }

            if (!match.PrincipalTable.Name.Equals(sqlFk.PrincipalTable, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{table}: FK {sqlFk.Name} principal table sql={sqlFk.PrincipalTable} ef={match.PrincipalTable.Name}.");
            }

            if (!ColumnsEqual(sqlFk.PrincipalColumns, match.PrincipalColumns.Select(c => c.Name)))
            {
                issues.Add($"{table}: FK {sqlFk.Name} principal columns sql=({Join(sqlFk.PrincipalColumns)}) ef=({Join(match.PrincipalColumns.Select(c => c.Name))}).");
            }
        }

        foreach (var efFk in efForeignKeys)
        {
            if (!def.ForeignKeys.Any(fk => fk.Name.Equals(efFk.Name, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add($"{table}: EF declares FK {efFk.Name} that is not in the SQL schema.");
            }
        }
    }

    private static void CompareUniques(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        var efUniques = ef.UniqueConstraints
            .Where(c => ef.PrimaryKey is null || !c.Name.Equals(ef.PrimaryKey.Name, StringComparison.OrdinalIgnoreCase))
            .Select(c => new SqlUnique(c.Name, c.Columns.Select(col => col.Name).ToArray()))
            .Concat(ef.Indexes.Where(i => i.IsUnique)
                .Select(i => new SqlUnique(i.Name, i.Columns.Select(c => c.Name).ToArray())))
            .ToArray();

        foreach (var unique in def.Uniques)
        {
            var match = efUniques.FirstOrDefault(u => u.Name.Equals(unique.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                issues.Add($"{table}: unique constraint/index {unique.Name} missing from the EF model.");
                continue;
            }

            if (!ColumnsEqual(unique.Columns, match.Columns))
            {
                issues.Add($"{table}: unique {unique.Name} columns sql=({Join(unique.Columns)}) ef=({Join(match.Columns)}).");
            }
        }

        foreach (var efUnique in efUniques)
        {
            if (!def.Uniques.Any(u => u.Name.Equals(efUnique.Name, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add($"{table}: EF declares unique constraint/index {efUnique.Name} that is not in the SQL schema.");
            }
        }
    }

    private static void CompareChecks(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        // Store-level CHECK constraints are owned by the authoritative SQL script and
        // enforced by SQL Server itself; the EF model must not contradict or add extras.
        foreach (var efCheck in ef.CheckConstraints)
        {
            if (efCheck.Name is null)
            {
                issues.Add($"{table}: EF declares an unnamed CHECK constraint.");
                continue;
            }

            var scriptCheck = def.Checks.FirstOrDefault(c => c.Name.Equals(efCheck.Name, StringComparison.OrdinalIgnoreCase));
            if (scriptCheck is null)
            {
                issues.Add($"{table}: EF declares CHECK {efCheck.Name} that is not in the SQL schema.");
            }
            else if (!SameDefinition(efCheck.Sql, scriptCheck.Definition))
            {
                issues.Add($"{table}: CHECK {efCheck.Name} sql='{scriptCheck.Definition}' ef='{efCheck.Sql}'.");
            }
        }
    }

    private static void CompareIndexes(string table, SqlTableDef def, ITable ef, List<string> issues)
    {
        var efIndexes = ef.Indexes.Where(i => !i.IsUnique).ToArray();

        foreach (var sqlIndex in def.Indexes)
        {
            var match = efIndexes.FirstOrDefault(i => i.Name.Equals(sqlIndex.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                issues.Add($"{table}: index {sqlIndex.Name} missing from the EF model.");
                continue;
            }

            if (!ColumnsEqual(sqlIndex.Columns, match.Columns.Select(c => c.Name)))
            {
                issues.Add($"{table}: index {sqlIndex.Name} columns sql=({Join(sqlIndex.Columns)}) ef=({Join(match.Columns.Select(c => c.Name))}).");
            }
        }

        foreach (var efIndex in efIndexes)
        {
            if (!def.Indexes.Any(i => i.Name.Equals(efIndex.Name, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add($"{table}: EF declares index {efIndex.Name} that is not in the SQL schema.");
            }
        }
    }

    /// <summary>lowercase without spaces: <c>decimal(4, 2)</c> == <c>decimal(4,2)</c>.</summary>
    public static string Normalise(string type) =>
        Regex.Replace((type ?? string.Empty).ToLowerInvariant(), @"\s+", string.Empty);

    private static bool SameDefinition(string efSql, string scriptSql)
    {
        static string canon(string text) =>
            Regex.Replace((text ?? string.Empty).Replace("'", "\"").ToLowerInvariant(), @"\s+", string.Empty);

        return canon(efSql).Equals(canon(scriptSql), StringComparison.Ordinal);
    }

    private static bool ColumnsEqual(IEnumerable<string> left, IEnumerable<string> right) =>
        left.TrimBrackets().SequenceEqual(right.TrimBrackets(), StringComparer.OrdinalIgnoreCase);

    private static string Spark(bool value) => value ? "NULL" : "NOT NULL";

    private static string Join(IEnumerable<string> columns) => string.Join(", ", columns);

    private static IEnumerable<string> TrimBrackets(this IEnumerable<string> source) =>
        source.Select(c => c.Trim('[', ']', '"', ' '));
}

