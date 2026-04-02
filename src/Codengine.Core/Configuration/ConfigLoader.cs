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

    /// <summary>
    /// Encodage des fichiers SQL extraits (ex: "utf-8", "iso-8859-1", "windows-1252").
    /// Par défaut : "utf-8".
    /// </summary>
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// Chemin vers l'exécutable Oracle SQLcl (optionnel).
    /// Exemple : "C:\\tools\\sqlcl\\bin\\sql.exe"
    /// </summary>
    public string? SqlclPath { get; set; }

    /// <summary>
    /// Options de formatage PL/SQL pour la commande format-oracle.
    /// </summary>
    public OracleFormatConfig Format { get; set; } = new();
}

public class OracleFormatConfig
{
    /// <summary>
    /// Nombre d'espaces par niveau d'indentation (défaut: 4).
    /// </summary>
    public int IndentSize { get; set; } = 4;

    /// <summary>
    /// Mettre les mots-clés PL/SQL en majuscules (défaut: true).
    /// </summary>
    public bool UppercaseKeywords { get; set; } = true;

    /// <summary>
    /// Nombre maximal de lignes vides consécutives conservées (défaut: 1).
    /// </summary>
    public int MaxConsecutiveBlankLines { get; set; } = 1;

    /// <summary>
    /// Supprimer les espaces en fin de ligne (défaut: true).
    /// </summary>
    public bool TrimTrailingWhitespace { get; set; } = true;

    /// <summary>
    /// Nombre de lignes vides entre les requêtes SQL (défaut: 1, utilisé par SqlFormatterNet).
    /// </summary>
    public int LinesBetweenQueries { get; set; } = 1;

    /// <summary>
    /// Longueur maximale des colonnes avant retour à la ligne (défaut: 0 = illimité, utilisé par SqlFormatterNet).
    /// </summary>
    public int MaxLineLength { get; set; } = 0;

    /// <summary>
    /// Moteur de formatage : "auto", "basic", "sqlformatternet", "sqlcl" (défaut: "auto").
    /// </summary>
    public string Engine { get; set; } = "auto";
}
