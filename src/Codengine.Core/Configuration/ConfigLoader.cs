using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codengine.Core.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] ConfigFileNames =
    {
        "codengine.config.json",
        "codengine.json",
        ".codengine.json"
    };

    public static async Task<CodengineConfig?> LoadAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var configPath = path ?? FindConfigFile();
        if (configPath == null || !File.Exists(configPath))
            return null;

        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<CodengineConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Erreur de syntaxe dans le fichier de configuration '{Path.GetFullPath(configPath)}': {ex.Message}" +
                " Vérifiez que les chemins utilisent des '/' ou des '\\\\' (double backslash).", ex);
        }
    }

    public static CodengineConfig? Load(string? path = null)
    {
        var configPath = path ?? FindConfigFile();
        if (configPath == null || !File.Exists(configPath))
            return null;

        var json = File.ReadAllText(configPath);
        try
        {
            return JsonSerializer.Deserialize<CodengineConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Erreur de syntaxe dans le fichier de configuration '{Path.GetFullPath(configPath)}': {ex.Message}" +
                " Vérifiez que les chemins utilisent des '/' ou des '\\\\' (double backslash).", ex);
        }
    }

    private static string? FindConfigFile()
    {
        var currentDir = Directory.GetCurrentDirectory();

        while (currentDir != null)
        {
            foreach (var fileName in ConfigFileNames)
            {
                var filePath = Path.Combine(currentDir, fileName);
                if (File.Exists(filePath))
                    return filePath;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        return null;
    }
}

public class CodengineConfig
{
    public string SourcePath { get; set; } = ".";
    public List<string> IncludePatterns { get; set; } = new() { "**/*.cs" };
    public List<string> ExcludePatterns { get; set; } = new() { "**/bin/**", "**/obj/**" };
    public Dictionary<string, RuleConfig> Rules { get; set; } = new();
    public ReportingConfig Reporting { get; set; } = new();
    public OracleConfig? Oracle { get; set; }
    public bool FailOnError { get; set; } = true;
    public bool FailOnWarning { get; set; } = false;
    public int MaxConcurrency { get; set; } = 0; // 0 = auto

    public EngineConfig ToEngineConfig()
    {
        return new EngineConfig
        {
            SourcePath = SourcePath,
            IncludePatterns = IncludePatterns,
            ExcludePatterns = ExcludePatterns,
            EnabledRuleIds = Rules.Where(r => r.Value.Enabled).Select(r => r.Key).ToList(),
            DisabledRuleIds = Rules.Where(r => !r.Value.Enabled).Select(r => r.Key).ToList(),
            FailOnError = FailOnError,
            FailOnWarning = FailOnWarning,
            MaxConcurrency = MaxConcurrency > 0 ? MaxConcurrency : Environment.ProcessorCount
        };
    }
}

public class RuleConfig
{
    public bool Enabled { get; set; } = true;
    public string? Severity { get; set; }
    public Dictionary<string, object>? Options { get; set; }
}

public class ReportingConfig
{
    public string Format { get; set; } = "console";
    public string? OutputPath { get; set; }
    public bool Verbose { get; set; } = false;
    public bool IncludeCodeSnippets { get; set; } = true;
}

public class OracleConfig
{
    public string? ConnectionString { get; set; }
    public string? Schema { get; set; }
    public string OutputDirectory { get; set; } = "./oracle_packages";
    public bool IncludePackageBodies { get; set; } = true;
    public List<string> IncludePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
}
