using Codengine.Core.Configuration;
using Codengine.Core.Engine;
using Codengine.Core.Models;
using Codengine.Reporters;
using Codengine.Rules.Abstractions;

namespace Codengine.Cli;

internal static class AnalyzeHandler
{
    private const string TagPrefix = "// codengine[";

    public static async Task RunAsync(
        string path,
        string? output,
        string format,
        bool verbose,
        string[] disabledRules,
        string? configPath,
        bool tag = false,
        bool untag = false)
    {
        if (untag)
        {
            Program.PrintHeader();
            Console.WriteLine($"Retrait des tags codengine dans: {Path.GetFullPath(path)}");
            Console.WriteLine();
            await RemoveTagsAsync(path);
            return;
        }

        Program.PrintHeader();
        Console.WriteLine($"Analyse de: {Path.GetFullPath(path)}");
        Console.WriteLine();

        // Charger la configuration
        var fileConfig = await ConfigLoader.LoadAsync(configPath);

        var provider = new DefaultRuleProvider();

        // Appliquer les règles désactivées depuis le fichier de config
        if (fileConfig?.Rules != null)
        {
            foreach (var (ruleId, ruleConfig) in fileConfig.Rules)
            {
                var rule = provider.GetRuleById(ruleId);
                if (rule != null)
                {
                    rule.IsEnabled = ruleConfig.Enabled;
                }
            }
        }

        // Appliquer les règles désactivées depuis la ligne de commande
        if (disabledRules.Length > 0)
        {
            var disabledSet = new HashSet<string>(
                disabledRules.SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var rule in provider.GetRules())
            {
                if (disabledSet.Contains(rule.Id))
                {
                    rule.IsEnabled = false;
                }
            }
        }

        var engine = new RoslynAnalysisEngine(provider);

        var config = fileConfig?.ToEngineConfig() ?? new EngineConfig();
        config.SourcePath = path;

        var result = await engine.AnalyzeAsync(config);

        IReporter reporter = format.ToLowerInvariant() switch
        {
            "json" => new JsonReporter(),
            "html" => new HtmlReporter(),
            _ => new ConsoleReporter()
        };

        var reporterOptions = new ReporterOptions
        {
            OutputPath = output,
            Verbose = verbose,
            IncludeCodeSnippets = fileConfig?.Reporting?.IncludeCodeSnippets ?? true
        };

        await reporter.ReportAsync(result, reporterOptions);

        if (tag && result.TotalViolations > 0)
        {
            Console.WriteLine();
            await ApplyTagsAsync(result);
        }

        Environment.ExitCode = result.HasErrors ? 1 : 0;
    }

    private static async Task ApplyTagsAsync(AnalysisResult result)
    {
        Console.WriteLine("Application des tags codengine...");
        var taggedCount = 0;

        foreach (var group in result.Violations.GroupBy(v => v.FilePath))
        {
            var filePath = group.Key;
            if (!File.Exists(filePath)) continue;

            var (content, encoding) = await FileEncodingHelper.ReadFileAsync(filePath);
            var lineEnding = content.Contains("\r\n") ? "\r\n" : "\n";
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Retirer les tags existants (idempotent)
            for (int i = 0; i < lines.Length; i++)
            {
                var idx = lines[i].IndexOf(TagPrefix, StringComparison.Ordinal);
                if (idx >= 0)
                    lines[i] = lines[i][..idx].TrimEnd();
            }

            // Ajouter les nouveaux tags
            foreach (var lineGroup in group.GroupBy(v => v.Line))
            {
                var lineIndex = lineGroup.Key - 1;
                if (lineIndex < 0 || lineIndex >= lines.Length) continue;

                var ruleIds = string.Join(", ", lineGroup.Select(v => v.RuleId).Distinct());
                lines[lineIndex] = lines[lineIndex].TrimEnd() + "  " + TagPrefix + ruleIds + "]";
            }

            await File.WriteAllTextAsync(filePath, string.Join(lineEnding, lines), encoding);
            taggedCount++;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Tagué: {filePath}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"{taggedCount} fichier(s) tagué(s). Utilisez --untag pour retirer les tags.");
    }

    private static async Task RemoveTagsAsync(string path)
    {
        IEnumerable<string> files;

        if (File.Exists(path))
        {
            files = new[] { path };
        }
        else if (Directory.Exists(path))
        {
            files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Chemin introuvable: {path}");
            Console.ResetColor();
            return;
        }

        var cleanedCount = 0;

        foreach (var filePath in files)
        {
            var (content, encoding) = await FileEncodingHelper.ReadFileAsync(filePath);
            if (!content.Contains(TagPrefix)) continue;

            var lineEnding = content.Contains("\r\n") ? "\r\n" : "\n";
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var idx = lines[i].IndexOf(TagPrefix, StringComparison.Ordinal);
                if (idx >= 0)
                    lines[i] = lines[i][..idx].TrimEnd();
            }

            await File.WriteAllTextAsync(filePath, string.Join(lineEnding, lines), encoding);
            cleanedCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Nettoyé: {filePath}");
            Console.ResetColor();
        }

        if (cleanedCount == 0)
            Console.WriteLine("Aucun tag codengine trouvé.");
        else
        {
            Console.WriteLine();
            Console.WriteLine($"{cleanedCount} fichier(s) nettoyé(s).");
        }
    }
}
