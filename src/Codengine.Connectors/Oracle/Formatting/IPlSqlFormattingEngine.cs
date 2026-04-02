namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Abstraction for PL/SQL formatting engines.
/// Each engine implements a different formatting strategy (basic indentation, SQL formatter, SQLcl).
/// </summary>
public interface IPlSqlFormattingEngine
{
    /// <summary>
    /// Engine display name used for logging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns true if this engine is available (e.g. external tool installed).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Formats the given PL/SQL code according to the specified options.
    /// </summary>
    /// <param name="sql">The PL/SQL source code to format.</param>
    /// <param name="options">Formatting options (indent size, keywords, etc.).</param>
    /// <returns>The formatted PL/SQL code.</returns>
    string Format(string sql, PlSqlFormatterOptions options);
}
