namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Fallback formatting engine based on the built-in state-machine PL/SQL formatter.
/// Handles block indentation (BEGIN/END, IF/THEN, LOOP, EXCEPTION, etc.)
/// without any external dependency.
/// </summary>
public sealed class BasicPlSqlFormattingEngine : IPlSqlFormattingEngine
{
    public string Name => "Basic (built-in)";

    public bool IsAvailable => true;

    public string Format(string sql, PlSqlFormatterOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var formatter = new PlSqlFormatter(options);
        var result = formatter.Format(sql);
        return result.FormattedCode;
    }
}
