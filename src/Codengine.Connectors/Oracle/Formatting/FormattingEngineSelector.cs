using System.Text.RegularExpressions;

namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Selects the appropriate formatting engine based on SQL content and available tools.
/// <list type="bullet">
/// <item>Complex PL/SQL (PACKAGE, PROCEDURE, FUNCTION, TRIGGER) → SQLcl if available, else Basic</item>
/// <item>Simple SQL (DML, DDL queries) → SqlFormatterNet for elegant query formatting</item>
/// <item>Fallback → Basic engine (always available)</item>
/// </list>
/// </summary>
public sealed partial class FormattingEngineSelector
{
    private readonly BasicPlSqlFormattingEngine _basicEngine = new();
    private readonly SqlFormatterNetEngine _sqlFormatterNetEngine = new();
    private readonly CombinedPlSqlFormattingEngine _combinedEngine = new();
    private readonly SqlclFormattingEngine _sqlclEngine;

    public FormattingEngineSelector(string? sqlclPath = null)
    {
        _sqlclEngine = new SqlclFormattingEngine(sqlclPath);
    }

    /// <summary>
    /// Selects the best available engine for the given SQL content.
    /// </summary>
    public IPlSqlFormattingEngine Select(string sql, FormattingEngineMode mode = FormattingEngineMode.Auto)
    {
        return mode switch
        {
            FormattingEngineMode.Basic => _basicEngine,
            FormattingEngineMode.SqlFormatterNet => _sqlFormatterNetEngine,
            FormattingEngineMode.Combined => _combinedEngine,
            FormattingEngineMode.Sqlcl when _sqlclEngine.IsAvailable => _sqlclEngine,
            FormattingEngineMode.Sqlcl => _basicEngine,
            FormattingEngineMode.Auto => SelectAuto(sql),
            _ => _basicEngine
        };
    }

    /// <summary>
    /// Formats the SQL using the best available engine, with automatic fallback on error.
    /// </summary>
    public FormattingResult Format(string sql, PlSqlFormatterOptions options, FormattingEngineMode mode = FormattingEngineMode.Auto)
    {
        var engine = Select(sql, mode);

        try
        {
            var formatted = engine.Format(sql, options);
            return new FormattingResult(formatted, engine.Name, FallbackUsed: false);
        }
        catch
        {
            // Fallback to basic engine if the selected engine fails
            if (engine != _basicEngine)
            {
                var formatted = _basicEngine.Format(sql, options);
                return new FormattingResult(formatted, _basicEngine.Name, FallbackUsed: true);
            }

            throw;
        }
    }

    private IPlSqlFormattingEngine SelectAuto(string sql)
    {
        if (LooksLikeFullPlSql(sql))
        {
            // For procedural PL/SQL: SQLcl (best quality) → Basic (reliable block indentation)
            return _sqlclEngine.IsAvailable ? _sqlclEngine : _basicEngine;
        }

        // For simple SQL queries: SqlFormatterNet handles DML/DDL elegantly
        return _sqlFormatterNetEngine;
    }

    /// <summary>
    /// Heuristic: returns true if the SQL contains procedural PL/SQL constructs.
    /// </summary>
    internal static bool LooksLikeFullPlSql(string sql)
    {
        return PlSqlPatternRegex().IsMatch(sql);
    }

    [GeneratedRegex(
        @"CREATE\s+(OR\s+REPLACE\s+)?(PACKAGE|PROCEDURE|FUNCTION|TRIGGER|TYPE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlSqlPatternRegex();
}

/// <summary>
/// Controls which formatting engine is used.
/// </summary>
public enum FormattingEngineMode
{
    /// <summary>Automatic selection based on SQL content and available tools.</summary>
    Auto,

    /// <summary>Built-in state-machine formatter (always available, best for PL/SQL blocks).</summary>
    Basic,

    /// <summary>Hogimn.Sql.Formatter NuGet (100% C#, best for SQL queries).</summary>
    SqlFormatterNet,

    /// <summary>Oracle SQLcl external tool (maximum quality, requires installation).</summary>
    Sqlcl,

    /// <summary>Basic for PL/SQL block indentation + SqlFormatterNet for embedded SQL queries.</summary>
    Combined
}

/// <summary>
/// Result of formatting with engine metadata.
/// </summary>
public sealed record FormattingResult(string FormattedCode, string EngineName, bool FallbackUsed);
