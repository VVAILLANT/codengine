using System.Text;
using Codengine.Connectors.Oracle;
using Codengine.Connectors.Oracle.Formatting;
using Codengine.Core.Configuration;

namespace Codengine.Cli;

internal static class FormatOracleHandler
{
    public static async Task RunAsync(
        string? path,
        bool dryRun,
        bool backup,
        int? indentSize,
        bool? uppercaseKeywords,
        string? engine,
        bool useConfig = false)
    {
        Program.PrintHeader();
        Console.WriteLine("Formatage PL/SQL Oracle");
        Console.WriteLine();

        // Charger la configuration si demandé
        OracleConfig? oracleConfig = null;
        if (useConfig)
        {
            try
            {
                var fileConfig = await ConfigLoader.LoadAsync();
                oracleConfig = fileConfig?.Oracle;
            }
            catch (InvalidOperationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }
        }

        if (oracleConfig != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Configuration chargée depuis codengine.config.json");
            Console.ResetColor();
        }

        // Résoudre le répertoire source : CLI > config > défaut
        var effectivePath = path
            ?? oracleConfig?.OutputDirectory
            ?? "./oracle_packages";

        if (!Directory.Exists(effectivePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Répertoire introuvable : {Path.GetFullPath(effectivePath)}");
            Console.ResetColor();
            Environment.ExitCode = 1;
            return;
        }

        // Construire les options du formateur : CLI > config > défaut
        var formatConfig = oracleConfig?.Format ?? new OracleFormatConfig();
        var engineMode = ParseEngineMode(engine ?? formatConfig.Engine);
        var options = new PlSqlFormatterOptions
        {
            IndentSize = indentSize ?? formatConfig.IndentSize,
            UppercaseKeywords = uppercaseKeywords ?? formatConfig.UppercaseKeywords,
            MaxConsecutiveBlankLines = formatConfig.MaxConsecutiveBlankLines,
            TrimTrailingWhitespace = formatConfig.TrimTrailingWhitespace,
            LinesBetweenQueries = formatConfig.LinesBetweenQueries,
            MaxLineLength = formatConfig.MaxLineLength,
            SqlclPath = oracleConfig?.SqlclPath,
            Engine = engineMode
        };

        var engineSelector = new FormattingEngineSelector(options.SqlclPath);
        var sqlFiles = Directory.GetFiles(effectivePath, "*.sql", SearchOption.AllDirectories);

        if (sqlFiles.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Aucun fichier .sql trouvé dans {Path.GetFullPath(effectivePath)}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Répertoire : {Path.GetFullPath(effectivePath)}");
        Console.WriteLine($"Fichiers   : {sqlFiles.Length} fichier(s) .sql");
        Console.WriteLine($"Indent     : {options.IndentSize} espaces");
        Console.WriteLine($"Keywords   : {(options.UppercaseKeywords ? "MAJUSCULES" : "inchangés")}");
        Console.WriteLine($"Moteur     : {engineMode}");
        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Mode dry-run : aucune modification ne sera appliquée");
            Console.ResetColor();
        }
        Console.WriteLine();

        int formatted = 0;
        int skipped = 0;
        int errors = 0;

        foreach (var filePath in sqlFiles)
        {
            var fileName = Path.GetRelativePath(effectivePath, filePath);

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var engineResult = engineSelector.Format(content, options, engineMode);

                // Vérification d'intégrité : le contenu non-whitespace doit être identique
                var integrityOk = VerifyIntegrity(content, engineResult.FormattedCode, options.UppercaseKeywords);

                if (!integrityOk)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ {fileName} — intégrité compromise, fichier ignoré");
                    Console.ResetColor();
                    errors++;
                    continue;
                }

                // Vérifier si le fichier a changé
                if (string.Equals(content, engineResult.FormattedCode, StringComparison.Ordinal))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  ─ {fileName} (déjà formaté)");
                    Console.ResetColor();
                    skipped++;
                    continue;
                }

                var originalLineCount = content.Split('\n').Length;
                var formattedLineCount = engineResult.FormattedCode.Split('\n').Length;

                if (dryRun)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    var engineInfo = engineResult.FallbackUsed ? $" [fallback → {engineResult.EngineName}]" : "";
                    Console.WriteLine($"  ~ {fileName} ({originalLineCount} → {formattedLineCount} lignes){engineInfo}");
                    Console.ResetColor();
                    formatted++;
                    continue;
                }

                // Backup si demandé
                if (backup)
                {
                    await File.WriteAllTextAsync(filePath + ".bak", content);
                }

                await File.WriteAllTextAsync(filePath, engineResult.FormattedCode);

                Console.ForegroundColor = ConsoleColor.Green;
                var fallbackInfo = engineResult.FallbackUsed ? $" [fallback → {engineResult.EngineName}]" : "";
                Console.WriteLine($"  ✓ {fileName} ({originalLineCount} → {formattedLineCount} lignes){fallbackInfo}");
                Console.ResetColor();
                formatted++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ {fileName} — {ex.Message}");
                Console.ResetColor();
                errors++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Résultat : {formatted} formaté(s), {skipped} inchangé(s), {errors} erreur(s)");

        if (errors > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Vérifie que le contenu non-whitespace est identique entre l'original et le formaté.
    /// </summary>
    private static bool VerifyIntegrity(string original, string formatted, bool uppercaseKeywords)
    {
        var originalContent = NormalizeContent(original);
        var formattedContent = NormalizeContent(formatted);

        var comparison = uppercaseKeywords
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(originalContent, formattedContent, comparison);
    }

    private static string NormalizeContent(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static FormattingEngineMode ParseEngineMode(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "auto" => FormattingEngineMode.Auto,
            "basic" => FormattingEngineMode.Basic,
            "sqlformatternet" => FormattingEngineMode.SqlFormatterNet,
            "combined" => FormattingEngineMode.Combined,
            "sqlcl" => FormattingEngineMode.Sqlcl,
            _ => FormattingEngineMode.Auto
        };
    }
}
