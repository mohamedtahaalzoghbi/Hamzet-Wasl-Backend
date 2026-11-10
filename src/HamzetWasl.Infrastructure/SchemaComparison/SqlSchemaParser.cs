using System.Text;
using System.Text.RegularExpressions;

namespace HamzetWasl.Infrastructure.SchemaComparison;

/// <summary>Parsed column definition from the authoritative SQL schema.</summary>
public sealed record SqlColumn(string Name, string Type, bool Nullable, bool Identity, bool Computed);

public sealed record SqlForeignKey(
    string Name,
    IReadOnlyList<string> Columns,
    string PrincipalTable,
    IReadOnlyList<string> PrincipalColumns);

public sealed record SqlUnique(string Name, IReadOnlyList<string> Columns);

public sealed record SqlCheck(string Name, string Definition);

public sealed record SqlIndex(string Name, IReadOnlyList<string> Columns, bool IsUnique);

public sealed record SqlTableDef(
    string Name,
    IReadOnlyList<SqlColumn> Columns,
    string? PrimaryKeyName,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyList<SqlUnique> Uniques,
    IReadOnlyList<SqlCheck> Checks,
    IReadOnlyList<SqlIndex> Indexes,
    IReadOnlyList<SqlForeignKey> ForeignKeys);

/// <summary>Result of parsing the whole script.</summary>
public sealed record SqlSchema(
    IReadOnlyDictionary<string, SqlTableDef> Tables,
    IReadOnlyList<SqlForeignKey> ForeignKeys)
{
    public int TableCount => Tables.Count;
}

/// <summary>
/// Parses Hamzet_Wasl_Schema_v2.sql into normalised metadata: the 60 domain tables
/// with their columns (name/type/nullability/identity), primary keys, unique
/// constraints (inline + CREATE UNIQUE INDEX), CHECK constraints, plain indexes
/// (CREATE INDEX) and foreign keys (ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY).
/// </summary>
public static partial class SqlSchemaParser
{
    private static readonly Regex CreateTableRegex = new(
        @"CREATE\s+TABLE\s+(?:\[dbo\]\.|dbo\.)\[?(\w+)\]?\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AlterForeignKeyRegex = new(
        @"ALTER\s+TABLE\s+(?:dbo\.|\[dbo\]\.)\[?(\w+)\]?\s+" +
        @"ADD\s+CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s*\(([^)]*)\)\s*" +
        @"REFERENCES\s+(?:dbo\.|\[dbo\]\.)\[?(\w+)\]?\s*\(([^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UniqueIndexRegex = new(
        @"CREATE\s+UNIQUE\s+INDEX\s+(\w+)\s+ON\s+(?:dbo\.|\[dbo\]\.)\[?(\w+)\]?\s*\(([^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IndexRegex = new(
        @"CREATE\s+INDEX\s+(\w+)\s+ON\s+(?:dbo\.|\[dbo\]\.)\[?(\w+)\]?\s*\(([^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Column name (first token), then the type keyword.</summary>
    [GeneratedRegex(@"^\[?(\w+)\]?", RegexOptions.Compiled)]
    private static partial Regex ColumnNameRegex();

    public static SqlSchema Parse(string script)
    {
        var text = RemoveComments(script);

        // Separate single-statement indexes.
        var uniqueIndexesByTable = new Dictionary<string, List<SqlUnique>>(StringComparer.OrdinalIgnoreCase);
        var indexesByTable = new Dictionary<string, List<SqlIndex>>(StringComparer.OrdinalIgnoreCase);
        var fksByOwner = new Dictionary<string, List<SqlForeignKey>>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in UniqueIndexRegex.Matches(text))
        {
            AddTo(uniqueIndexesByTable, m.Groups[2].Value, new SqlUnique(m.Groups[1].Value, SplitColumns(m.Groups[3].Value)));
        }

        foreach (Match m in IndexRegex.Matches(text))
        {
            AddTo(indexesByTable, m.Groups[2].Value, new SqlIndex(m.Groups[1].Value, SplitColumns(m.Groups[3].Value), false));
        }

        foreach (Match m in AlterForeignKeyRegex.Matches(text))
        {
            AddTo(fksByOwner, m.Groups[1].Value, new SqlForeignKey(
                m.Groups[2].Value,
                SplitColumns(m.Groups[3].Value),
                m.Groups[4].Value,
                SplitColumns(m.Groups[5].Value)));
        }

        var tables = new Dictionary<string, SqlTableDef>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CreateTableRegex.Matches(text))
        {
            var name = match.Groups[1].Value;
            var body = ScanBalancedBlock(text, match.Index + match.Value.Length);

            var columns = new List<SqlColumn>();
            string? pkName = null;
            var pkColumns = new List<string>();
            var uniques = new List<SqlUnique>();
            var checks = new List<SqlCheck>();

            foreach (var statement in SplitStatements(body))
            {
                if (statement.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                {
                    ParseConstraintStatement(statement, uniques, checks, pkColumns, ref pkName);
                    continue;
                }

                columns.Add(ParseColumnStatement(statement));
            }

            if (uniqueIndexesByTable.TryGetValue(name, out var scriptUniques))
            {
                uniques.AddRange(scriptUniques);
            }

            fksByOwner.TryGetValue(name, out var tableForeignKeys);
            indexesByTable.TryGetValue(name, out var tableIndexes);

            tables[name] = new SqlTableDef(
                name,
                columns,
                pkName,
                pkColumns,
                uniques,
                checks,
                tableIndexes ?? [],
                tableForeignKeys ?? []);
        }

        return new SqlSchema(tables, tables.Values.SelectMany(t => t.ForeignKeys).ToList());
    }

    private static void ParseConstraintStatement(
        string statement,
        List<SqlUnique> uniques,
        List<SqlCheck> checks,
        List<string> pkColumns,
        ref string? pkName)
    {
        // CONSTRAINT <name> <KIND> <rest>
        var head = Regex.Match(statement, @"^CONSTRAINT\s+(\w+)\s+", RegexOptions.IgnoreCase);
        if (!head.Success)
        {
            throw new FormatException("Malformed constraint statement: " + statement);
        }

        var name = head.Groups[1].Value;
        var rest = statement[head.Length..];

        if (StartsWithKeyword(rest, "PRIMARY", "KEY"))
        {
            pkColumns.AddRange(SplitColumns(ExtractParens(rest, "PRIMARY KEY")));
            pkName = name;
        }
        else if (StartsWithKeyword(rest, "UNIQUE"))
        {
            uniques.Add(new SqlUnique(name, SplitColumns(ExtractParens(rest, "UNIQUE"))));
        }
        else if (StartsWithKeyword(rest, "CHECK"))
        {
            checks.Add(new SqlCheck(name, CollapseWhitespace(ExtractUntilEnd(rest, "CHECK"))));
        }
        else if (StartsWithKeyword(rest, "FOREIGN", "KEY"))
        {
            // Inline FKs are declared via ALTER TABLE in this script; parse defensively for names/refs.
            // The comparison uses the ALTER TABLE pass; nothing to do here.
        }
        else
        {
            throw new FormatException($"Unhandled constraint kind in: {statement}");
        }
    }

    private static bool StartsWithKeyword(string text, params string[] words)
    {
        var offset = 0;
        foreach (var word in words)
        {
            var match = Regex.Match(text[offset..], $@"^\s*{word}\b", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            offset += match.Length;
        }

        return true;
    }

    private static string ExtractParens(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var paren = text.IndexOf('(', index < 0 ? 0 : index + marker.Length);
        if (paren < 0)
        {
            throw new FormatException("Expected parentheses after: " + marker);
        }

        var close = text.IndexOf(')', paren + 1);
        return text[(paren + 1)..close];
    }

    /// <summary>CHECK definitions may contain parentheses; take everything after the marker.</summary>
    private static string ExtractUntilEnd(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return text[(index + marker.Length)..].Trim();
    }

    private static SqlColumn ParseColumnStatement(string statement)
    {
        var nameMatch = ColumnNameRegex().Match(statement);
        if (!nameMatch.Success)
        {
            throw new FormatException("Cannot read column name from: " + statement);
        }

        var columnName = nameMatch.Groups[1].Value;

        var keyword = Regex.Match(
            statement[(nameMatch.Length)..],
            @"^\s*(\w+)(\s*\(\s*(?<args>max|\d+(?:\s*,\s*\d+)?)\s*\))?",
            RegexOptions.IgnoreCase);
        if (!keyword.Success)
        {
            throw new FormatException("Cannot read column type from: " + statement);
        }

        var typeName = keyword.Groups[1].Value;
        var args = keyword.Groups["args"].Success ? keyword.Groups["args"].Value : null;
        var type = args is null ? typeName : $"{typeName}({args})";

        var nullable = !Regex.IsMatch(statement, @"\bNOT\s+NULL\b", RegexOptions.IgnoreCase);
        var identity = Regex.IsMatch(statement, @"\bIDENTITY\s*\(", RegexOptions.IgnoreCase);
        var computed = Regex.IsMatch(statement[(nameMatch.Length)..], @"^\s*AS\s*\(", RegexOptions.IgnoreCase);

        return new SqlColumn(columnName, type, nullable, identity, computed);
    }

    private static IReadOnlyList<string> SplitColumns(string text) => text
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(c => c.Trim('[', ']', ' ', '\t', '\r', '\n')).ToArray();

    private static string CollapseWhitespace(string text) => Regex.Replace(text, @"\s+", " ");

    private static void AddTo<TKey, TValue>(Dictionary<TKey, List<TValue>> store, TKey key, TValue value)
        where TKey : notnull
    {
        if (!store.TryGetValue(key, out var list))
        {
            list = [];
            store[key] = list;
        }

        list.Add(value);
    }

    /// <summary>Grabs the text between the paren at start and its balanced close.</summary>
    private static string ScanBalancedBlock(string text, int start)
    {
        var builder = new StringBuilder();
        var depth = 1;
        var inQuotes = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                builder.Append(ch);
                continue;
            }

            if (!inQuotes)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SplitStatements(string body)
    {
        var builder = new StringBuilder();
        var depth = 0;
        var inQuotes = false;
        foreach (var ch in body)
        {
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                }
                else if (depth == 0 && ch == ',')
                {
                    var statement = builder.ToString().Trim();
                    if (statement.Length > 0)
                    {
                        yield return statement;
                    }

                    builder.Clear();
                    continue;
                }
            }

            builder.Append(ch);
        }

        var tail = builder.ToString().Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }

    /// <summary>Removes line (`--`) and block comments without touching quoted strings.</summary>
    private static string RemoveComments(string script)
    {
        var builder = new StringBuilder(script.Length);
        var inQuotes = false;
        var i = 0;
        while (i < script.Length)
        {
            var ch = script[i];
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                builder.Append(ch);
                i++;
                continue;
            }

            if (!inQuotes && i + 1 < script.Length && ch == '-' && script[i + 1] == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (!inQuotes && i + 1 < script.Length && ch == '/' && script[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }

                i += 2;
                continue;
            }

            builder.Append(ch);
            i++;
        }

        return builder.ToString();
    }
}

