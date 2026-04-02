using System.Text;
using System.Text.RegularExpressions;

namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Hybrid formatting engine that combines Basic (PL/SQL block indentation) with
/// SqlFormatterNet (SQL query formatting). Runs Basic first to get correct block
/// indentation, then identifies embedded SQL DML statements and reformats them
/// with SqlFormatterNet while preserving the surrounding indentation level.
/// </summary>
public sealed partial class CombinedPlSqlFormattingEngine : IPlSqlFormattingEngine
{
    private readonly BasicPlSqlFormattingEngine _basicEngine = new();
    private readonly SqlFormatterNetEngine _sqlFormatterNetEngine = new();

    public string Name => "Combined (Basic + SqlFormatterNet)";

    public bool IsAvailable => true;

    public string Format(string sql, PlSqlFormatterOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);

        // Pass 1: Basic formatting for PL/SQL block indentation
        var basicFormatted = _basicEngine.Format(sql, options);

        // Pass 2: Re-format embedded SQL DML statements with SqlFormatterNet
        return ReformatEmbeddedSql(basicFormatted, options);
    }

    /// <summary>
    /// Scans the Basic-formatted output for standalone SQL DML statements
    /// (SELECT, INSERT, UPDATE, DELETE, MERGE) and reformats them with SqlFormatterNet.
    /// </summary>
    private string ReformatEmbeddedSql(string formattedSql, PlSqlFormatterOptions options)
    {
        var lines = formattedSql.Split('\n');
        var result = new StringBuilder();
        var inBlockComment = false;

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Track block comments
            if (!inBlockComment && trimmed.StartsWith("/*"))
            {
                inBlockComment = true;
            }

            if (inBlockComment)
            {
                result.Append(line).Append('\n');
                if (trimmed.Contains("*/"))
                {
                    inBlockComment = false;
                }

                i++;
                continue;
            }

            // Skip line comments
            if (trimmed.StartsWith("--"))
            {
                result.Append(line).Append('\n');
                i++;
                continue;
            }

            // Detect SQL DML statement start
            if (IsSqlDmlStart(trimmed))
            {
                var indent = GetIndentation(line);
                var (statementLines, endIndex) = CollectStatement(lines, i);
                var rawSql = string.Join("\n", statementLines.Select(l => l.TrimStart()));

                try
                {
                    var formatted = _sqlFormatterNetEngine.Format(rawSql, options);
                    var reindented = ReindentSql(formatted, indent);
                    result.Append(reindented);
                    // AppendLine is handled by ReindentSql adding \n
                }
                catch
                {
                    // If SqlFormatterNet fails, keep the Basic-formatted version
                    for (var j = i; j <= endIndex; j++)
                    {
                        result.Append(lines[j]).Append('\n');
                    }
                }

                i = endIndex + 1;
                continue;
            }

            result.Append(line).Append('\n');
            i++;
        }

        // Remove trailing newline to match Basic output behavior
        var text = result.ToString();
        if (text.EndsWith("\r\n"))
            text = text[..^2];
        else if (text.EndsWith('\n'))
            text = text[..^1];

        return text;
    }

    /// <summary>
    /// Returns true if the trimmed line starts a SQL DML statement (not a PL/SQL control-flow keyword).
    /// </summary>
    private static bool IsSqlDmlStart(string trimmedLine)
    {
        return SqlDmlStartRegex().IsMatch(trimmedLine);
    }

    /// <summary>
    /// Collects all lines belonging to a single SQL statement, starting at the given index.
    /// A statement ends when a line ends with ';' (outside strings).
    /// </summary>
    private static (List<string> Lines, int EndIndex) CollectStatement(string[] allLines, int startIndex)
    {
        var lines = new List<string>();
        var inString = false;

        for (var i = startIndex; i < allLines.Length; i++)
        {
            var line = allLines[i];
            lines.Add(line);

            // Track string state to avoid matching ';' inside strings
            foreach (char c in line)
            {
                if (c == '\'')
                    inString = !inString;
            }

            // Statement ends when line ends with ';' (not inside a string)
            if (!inString && line.TrimEnd().EndsWith(';'))
            {
                return (lines, i);
            }
        }

        // If we reach the end without finding ';', return all collected lines
        return (lines, allLines.Length - 1);
    }

    /// <summary>
    /// Returns the leading whitespace of a line.
    /// </summary>
    private static string GetIndentation(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] == ' ')
            i++;
        return line[..i];
    }

    /// <summary>
    /// Re-indents each line of formatted SQL with the given indentation prefix.
    /// </summary>
    private static string ReindentSql(string formattedSql, string indent)
    {
        var lines = formattedSql.Split('\n');
        var sb = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append('\n');
            }
            else
            {
                sb.Append(indent).Append(line).Append('\n');
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(
        @"^(SELECT|INSERT|UPDATE|DELETE|MERGE|WITH)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlDmlStartRegex();
}
