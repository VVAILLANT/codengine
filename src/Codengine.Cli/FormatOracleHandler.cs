using System.Text;
using Codengine.Connectors.Oracle;
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
        var options = new PlSqlFormatterOptions
        {
            IndentSize = indentSize ?? formatConfig.IndentSize,
            UppercaseKeywords = uppercaseKeywords ?? formatConfig.UppercaseKeywords,
            MaxConsecutiveBlankLines = formatConfig.MaxConsecutiveBlankLines,
            TrimTrailingWhitespace = formatConfig.TrimTrailingWhitespace
        };

        var formatter = new PlSqlFormatter(options);
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
                var result = formatter.Format(content);

                if (!result.IsIntegrityValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ {fileName} — intégrité compromise, fichier ignoré");
                    Console.ResetColor();
                    errors++;
                    continue;
                }

                // Vérifier si le fichier a changé
                if (string.Equals(content, result.FormattedCode, StringComparison.Ordinal))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  ─ {fileName} (déjà formaté)");
                    Console.ResetColor();
                    skipped++;
                    continue;
                }

                if (dryRun)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  ~ {fileName} ({result.OriginalLineCount} → {result.FormattedLineCount} lignes)");
                    Console.ResetColor();
                    formatted++;
                    continue;
                }

                // Backup si demandé
                if (backup)
                {
                    await File.WriteAllTextAsync(filePath + ".bak", content);
                }

                await File.WriteAllTextAsync(filePath, result.FormattedCode);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ {fileName} ({result.OriginalLineCount} → {result.FormattedLineCount} lignes)");
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
}
