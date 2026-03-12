using Codengine.Core.Models;

namespace Codengine.Reporters;

public class ConsoleReporter : IReporter
{
    public string Name => "console";

    public Task ReportAsync(AnalysisResult result, ReporterOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                    CODENGINE - ANALYSE TERMINÉE               ");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        Console.WriteLine($"  Source analysée : {result.SourcePath}");
        Console.WriteLine($"  Fichiers analysés : {result.FilesAnalyzed}");
        Console.WriteLine($"  Durée : {result.Duration.TotalSeconds:F2}s");
        Console.WriteLine();

        if (result.Violations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Aucune violation détectée !");
            Console.ResetColor();
            Console.WriteLine();
            return Task.CompletedTask;
        }

        // Résumé
        Console.WriteLine("  RÉSUMÉ:");
        WriteWithColor($"    Critiques : {result.Criticals}", result.Criticals > 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        WriteWithColor($"    Erreurs   : {result.Errors}", result.Errors > 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        WriteWithColor($"    Warnings  : {result.Warnings}", result.Warnings > 0 ? ConsoleColor.Yellow : ConsoleColor.Gray);
        Console.WriteLine();

        // Détails des violations
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine("  VIOLATIONS:");
        Console.WriteLine("───────────────────────────────────────────────────────────────");

        var groupedByFile = result.Violations.GroupBy(v => v.FilePath);

        foreach (var fileGroup in groupedByFile)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {fileGroup.Key}");
            Console.ResetColor();

            foreach (var violation in fileGroup.OrderBy(v => v.Line))
            {
                var color = violation.Severity switch
                {
                    RuleSeverity.Critical => ConsoleColor.Red,
                    RuleSeverity.Error => ConsoleColor.Red,
                    RuleSeverity.Warning => ConsoleColor.Yellow,
                    _ => ConsoleColor.Gray
                };

                var icon = violation.Severity switch
                {
                    RuleSeverity.Critical => "X",
                    RuleSeverity.Error => "X",
                    RuleSeverity.Warning => "!",
                    _ => "i"
                };

                Console.ForegroundColor = color;
                Console.Write($"    {icon} ");
                Console.ResetColor();
                Console.WriteLine($"[{violation.RuleId}] Ligne {violation.Line}: {violation.Message}");

                if (options.IncludeCodeSnippets && !string.IsNullOrEmpty(violation.CodeSnippet))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    var snippet = violation.CodeSnippet.Length > 80
                        ? violation.CodeSnippet.Substring(0, 77) + "..."
                        : violation.CodeSnippet;
                    Console.WriteLine($"      > {snippet.Replace("\n", " ").Replace("\r", "")}");
                    Console.ResetColor();
                }

                if (options.Verbose && !string.IsNullOrEmpty(violation.SuggestedFix))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"      Suggestion: {violation.SuggestedFix}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        if (result.HasErrors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ÉCHEC: {result.TotalViolations} violation(s) détectée(s)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  AVERTISSEMENT: {result.TotalViolations} avertissement(s)");
        }
        Console.ResetColor();
        Console.WriteLine();

        return Task.CompletedTask;
    }

    private static void WriteWithColor(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
