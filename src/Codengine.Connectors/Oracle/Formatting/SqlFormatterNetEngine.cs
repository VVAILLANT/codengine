using SQL.Formatter;
using SQL.Formatter.Core;
using SQL.Formatter.Language;

namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Formatting engine based on the Hogimn.Sql.Formatter NuGet package (100% C#, no external dependency).
/// Best suited for simple SQL scripts (SELECT, INSERT, DML). For complex PL/SQL procedural
/// blocks (packages, procedures with BEGIN/END), prefer <see cref="BasicPlSqlFormattingEngine"/>.
/// </summary>
public sealed class SqlFormatterNetEngine : IPlSqlFormattingEngine
{
    public string Name => "SqlFormatterNet (Hogimn.Sql.Formatter)";

    public bool IsAvailable => true;

    public string Format(string sql, PlSqlFormatterOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var cfg = FormatConfig.Builder()
            .Indent(new string(' ', options.IndentSize))
            .Uppercase(options.UppercaseKeywords)
            .LinesBetweenQueries(options.LinesBetweenQueries)
            .MaxColumnLength(options.MaxLineLength)
            .Build();

        var formatted = SqlFormatter
            .Of(Dialect.PlSql)
            .Format(sql, cfg);

        if (options.TrimTrailingWhitespace)
        {
            var lines = formatted.Split('\n');
            formatted = string.Join("\n", lines.Select(l => l.TrimEnd()));
        }

        return formatted;
    }
}
