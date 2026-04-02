using System.Diagnostics;

namespace Codengine.Connectors.Oracle.Formatting;

/// <summary>
/// Formatting engine that delegates to Oracle SQLcl for maximum-quality formatting.
/// Requires SQLcl to be installed and configured via <see cref="PlSqlFormatterOptions.SqlclPath"/>.
/// When unavailable, <see cref="FormattingEngineSelector"/> falls back to another engine.
/// </summary>
public sealed class SqlclFormattingEngine : IPlSqlFormattingEngine
{
    public string Name => "SQLcl (Oracle)";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_sqlclPath) && File.Exists(_sqlclPath);

    private readonly string? _sqlclPath;

    public SqlclFormattingEngine(string? sqlclPath)
    {
        _sqlclPath = sqlclPath;
    }

    public string Format(string sql, PlSqlFormatterOptions options)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                $"SQLcl is not available. Path: '{_sqlclPath}'. Install SQLcl or use a different formatting engine.");
        }

        var tempInput = Path.Combine(Path.GetTempPath(), $"codengine_fmt_{Guid.NewGuid():N}.sql");
        var tempScript = Path.Combine(Path.GetTempPath(), $"codengine_script_{Guid.NewGuid():N}.sql");

        try
        {
            File.WriteAllText(tempInput, sql);
            File.WriteAllText(tempScript, BuildSqlclScript(tempInput, options));

            var psi = new ProcessStartInfo
            {
                FileName = _sqlclPath!,
                Arguments = $"/nolog @\"{tempScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start SQLcl process.");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit(TimeSpan.FromSeconds(30));

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"SQLcl exited with code {process.ExitCode}. Error: {error}");
            }

            // SQLcl rewrites the file in-place when using FORMAT FILE
            if (File.Exists(tempInput))
            {
                return File.ReadAllText(tempInput);
            }

            return output;
        }
        finally
        {
            TryDelete(tempInput);
            TryDelete(tempScript);
        }
    }

    private static string BuildSqlclScript(string inputFile, PlSqlFormatterOptions options)
    {
        return $"""
            SET SQLFORMAT ANSICONSOLE
            FORMAT FILE "{inputFile}"
            EXIT
            """;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
