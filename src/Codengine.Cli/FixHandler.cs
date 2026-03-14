using Codengine.Core.Configuration;
using Codengine.Core.Engine;
using Codengine.Core.Fixes;
using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Codengine.Rules.Fixes;

namespace Codengine.Cli;

internal static class FixHandler
{
    public static async Task RunAsync(string path, bool dryRun, string[] rules)
    {
        Program.PrintHeader();
        Console.WriteLine($"Correction de: {Path.GetFullPath(path)}");
        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Mode dry-run: aucune modification ne sera effectuée.");
            Console.ResetColor();
        }
        Console.WriteLine();

        var provider = new DefaultRuleProvider();
        var engine = new RoslynAnalysisEngine(provider);

        var config = new EngineConfig
        {
            SourcePath = path,
            IncludePatterns = new List<string> { "**/*.cs" },
            ExcludePatterns = new List<string> { "**/bin/**", "**/obj/**" }
        };

        // Analyser d'abord
        Console.WriteLine("Analyse en cours...");
        var result = await engine.AnalyzeAsync(config);

        if (result.TotalViolations == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Aucune violation à corriger.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Trouvé {result.TotalViolations} violation(s).");
        Console.WriteLine();

        // Créer le moteur de fix
        var fixers = new ICodeFixer[]
        {
            new NullCheckFixer(),
            new EmptyCatchFixer(),
            new AsyncNamingFixer()
        };

        var fixEngine = new CodeFixerEngine(fixers);

        // Filtrer par règles si spécifié
        var violations = result.Violations.AsEnumerable();
        if (rules.Length > 0)
        {
            var ruleSet = new HashSet<string>(rules, StringComparer.OrdinalIgnoreCase);
            violations = violations.Where(v => ruleSet.Contains(v.RuleId));
        }

        // Filtrer seulement les violations avec un fixer
        var fixableViolations = violations.Where(v => fixEngine.HasFixer(v.RuleId)).ToList();

        if (fixableViolations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Aucune violation n'a de correction automatique disponible.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"{fixableViolations.Count} violation(s) peuvent être corrigées automatiquement.");

        if (dryRun)
        {
            Console.WriteLine();
            foreach (var v in fixableViolations)
            {
                Console.WriteLine($"  [{v.RuleId}] {v.FilePath}:{v.Line} - {v.Message}");
            }
            return;
        }

        Console.WriteLine("Application des corrections...");
        Console.WriteLine();

        var summaries = await fixEngine.FixAllAsync(new AnalysisResult
        {
            SourcePath = path,
            AnalyzedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Violations = fixableViolations,
            FilesAnalyzed = 0
        });

        var totalFixed = summaries.Sum(s => s.Fixed);
        var totalFailed = summaries.Sum(s => s.Failed);

        foreach (var summary in summaries.Where(s => s.Modified))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Corrigé: {summary.FilePath} ({summary.Fixed} correction(s))");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"Résumé: {totalFixed} correction(s) appliquée(s), {totalFailed} échec(s).");

        if (summaries.Any(s => s.Errors.Count > 0))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nErreurs:");
            foreach (var error in summaries.SelectMany(s => s.Errors))
            {
                Console.WriteLine($"  {error}");
            }
            Console.ResetColor();
        }
    }
}
