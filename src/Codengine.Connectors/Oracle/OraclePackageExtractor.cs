using System.Text;
using Codengine.Connectors.Abstractions;
using Oracle.ManagedDataAccess.Client;

namespace Codengine.Connectors.Oracle;

public class OraclePackageExtractorConfig
{
    public required string ConnectionString { get; init; }
    public string? Schema { get; init; }
    public string OutputDirectory { get; init; } = "./oracle_packages";
    public bool IncludePackageBodies { get; init; } = true;
    public List<string> IncludePatterns { get; init; } = new();
    public List<string> ExcludePatterns { get; init; } = new();
    public Encoding Encoding { get; init; } = Encoding.UTF8;
}

public class OraclePackageExtractor : ISourceConnector
{
    private readonly OraclePackageExtractorConfig _config;

    public string Name => "Oracle";

    public OraclePackageExtractor(OraclePackageExtractorConfig config)
    {
        _config = config;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new OracleConnection(_config.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<SourceFile>> ExtractSourcesAsync(CancellationToken cancellationToken = default)
    {
        var packages = new List<SourceFile>();

        await using var connection = new OracleConnection(_config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var schema = _config.Schema ?? GetCurrentSchema(connection);

        // Récupérer la liste des packages
        var packageNames = await GetPackageNamesAsync(connection, schema, cancellationToken);

        foreach (var packageName in packageNames)
        {
            if (!ShouldIncludePackage(packageName))
                continue;

            var packageContent = await ExtractPackageAsync(connection, schema, packageName, cancellationToken);
            if (packageContent != null)
            {
                packages.Add(packageContent);
            }
        }

        return packages;
    }

    public async Task ExtractAndSaveAsync(CancellationToken cancellationToken = default)
    {
        var sources = await ExtractSourcesAsync(cancellationToken);

        if (!Directory.Exists(_config.OutputDirectory))
        {
            Directory.CreateDirectory(_config.OutputDirectory);
        }

        foreach (var source in sources)
        {
            var fileName = $"{source.Name}.sql";
            var filePath = Path.Combine(_config.OutputDirectory, fileName);
            await File.WriteAllTextAsync(filePath, source.Content, _config.Encoding, cancellationToken);
            Console.WriteLine($"Extrait: {fileName}");
        }

        Console.WriteLine($"\n{sources.Count()} package(s) extraits vers {_config.OutputDirectory}");
    }

    private async Task<List<string>> GetPackageNamesAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var packages = new List<string>();

        const string sql = @"
            SELECT DISTINCT OBJECT_NAME
            FROM ALL_OBJECTS
            WHERE OWNER = :schema
              AND OBJECT_TYPE = 'PACKAGE'
            ORDER BY OBJECT_NAME";

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(new OracleParameter("schema", schema));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            packages.Add(reader.GetString(0));
        }

        return packages;
    }

    private async Task<SourceFile?> ExtractPackageAsync(
        OracleConnection connection,
        string schema,
        string packageName,
        CancellationToken cancellationToken)
    {
        var content = new StringBuilder();
        content.AppendLine($"-- Package: {packageName}");
        content.AppendLine($"-- Schema: {schema}");
        content.AppendLine($"-- Extracted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        content.AppendLine();

        // Extraire le header (specification)
        var header = await GetSourceCodeAsync(connection, schema, packageName, "PACKAGE", cancellationToken);
        if (string.IsNullOrEmpty(header))
            return null;

        content.AppendLine("-- ═══════════════════════════════════════════════════════════════");
        content.AppendLine("-- PACKAGE SPECIFICATION");
        content.AppendLine("-- ═══════════════════════════════════════════════════════════════");
        content.AppendLine();
        content.AppendLine($"CREATE OR REPLACE {header}");
        content.AppendLine("/");
        content.AppendLine();

        // Extraire le body si demandé
        if (_config.IncludePackageBodies)
        {
            var body = await GetSourceCodeAsync(connection, schema, packageName, "PACKAGE BODY", cancellationToken);
            if (!string.IsNullOrEmpty(body))
            {
                content.AppendLine("-- ═══════════════════════════════════════════════════════════════");
                content.AppendLine("-- PACKAGE BODY");
                content.AppendLine("-- ═══════════════════════════════════════════════════════════════");
                content.AppendLine();
                content.AppendLine($"CREATE OR REPLACE {body}");
                content.AppendLine("/");
            }
        }

        return new SourceFile
        {
            Name = packageName,
            Content = content.ToString(),
            Type = "PLSQL_PACKAGE",
            Metadata = new Dictionary<string, string>
            {
                { "Schema", schema },
                { "HasBody", _config.IncludePackageBodies.ToString() }
            }
        };
    }

    private async Task<string?> GetSourceCodeAsync(
        OracleConnection connection,
        string schema,
        string objectName,
        string objectType,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TEXT
            FROM ALL_SOURCE
            WHERE OWNER = :schema
              AND NAME = :objectName
              AND TYPE = :objectType
            ORDER BY LINE";

        var sourceBuilder = new StringBuilder();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(new OracleParameter("schema", schema));
        command.Parameters.Add(new OracleParameter("objectName", objectName));
        command.Parameters.Add(new OracleParameter("objectType", objectType));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sourceBuilder.Append(reader.GetString(0));
        }

        var source = sourceBuilder.ToString();
        return string.IsNullOrWhiteSpace(source) ? null : source;
    }

    private bool ShouldIncludePackage(string packageName)
    {
        // Si des patterns d'inclusion sont définis, le package doit matcher
        if (_config.IncludePatterns.Count > 0)
        {
            var matchesInclude = _config.IncludePatterns.Any(pattern =>
                MatchesPattern(packageName, pattern));
            if (!matchesInclude)
                return false;
        }

        // Si des patterns d'exclusion sont définis, le package ne doit pas matcher
        if (_config.ExcludePatterns.Count > 0)
        {
            var matchesExclude = _config.ExcludePatterns.Any(pattern =>
                MatchesPattern(packageName, pattern));
            if (matchesExclude)
                return false;
        }

        return true;
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        // Support basique des wildcards (* et ?)
        var regexPattern = "^" +
            pattern
                .Replace(".", "\\.")
                .Replace("*", ".*")
                .Replace("?", ".") +
            "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            name,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string GetCurrentSchema(OracleConnection connection)
    {
        using var command = new OracleCommand("SELECT USER FROM DUAL", connection);
        var result = command.ExecuteScalar();
        return result?.ToString()?.ToUpperInvariant() ?? "UNKNOWN";
    }
}
