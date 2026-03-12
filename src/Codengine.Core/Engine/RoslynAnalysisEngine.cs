using System.Collections.Concurrent;
using Codengine.Core.Configuration;
using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codengine.Core.Engine;

public class RoslynAnalysisEngine : IAnalysisEngine
{
    private readonly IRuleProvider _ruleProvider;

    public RoslynAnalysisEngine(IRuleProvider ruleProvider)
    {
        _ruleProvider = ruleProvider;
    }

    public async Task<AnalysisResult> AnalyzeAsync(EngineConfig config, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var violations = new ConcurrentBag<Violation>();

        var files = GetFiles(config);
        var rules = _ruleProvider.GetRules().ToList();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.MaxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(files, options, async (file, ct) =>
        {
            var fileViolations = await AnalyzeFileInternalAsync(file, rules, ct);
            foreach (var violation in fileViolations)
            {
                violations.Add(violation);
            }
        });

        return new AnalysisResult
        {
            SourcePath = config.SourcePath,
            AnalyzedAt = startTime,
            Duration = DateTime.UtcNow - startTime,
            Violations = violations.OrderBy(v => v.FilePath).ThenBy(v => v.Line).ToList(),
            FilesAnalyzed = files.Count
        };
    }

    public async Task<AnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var rules = _ruleProvider.GetRules().ToList();
        var violations = await AnalyzeFileInternalAsync(filePath, rules, cancellationToken);

        return new AnalysisResult
        {
            SourcePath = filePath,
            AnalyzedAt = startTime,
            Duration = DateTime.UtcNow - startTime,
            Violations = violations.ToList(),
            FilesAnalyzed = 1
        };
    }

    public async Task<AnalysisResult> AnalyzeCodeAsync(string code, string virtualFilePath = "code.cs", CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var rules = _ruleProvider.GetRules().ToList();

        var tree = CSharpSyntaxTree.ParseText(code, path: virtualFilePath, cancellationToken: cancellationToken);
        var compilation = CreateCompilation(new[] { tree });
        var semanticModel = compilation.GetSemanticModel(tree);

        var context = new RuleContext
        {
            SyntaxTree = tree,
            SemanticModel = semanticModel,
            FilePath = virtualFilePath,
            Compilation = compilation
        };

        var violations = new List<Violation>();
        foreach (var rule in rules)
        {
            try
            {
                violations.AddRange(rule.Analyze(context));
            }
            catch (Exception ex)
            {
                violations.Add(new Violation
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Message = $"Erreur lors de l'exécution de la règle: {ex.Message}",
                    FilePath = virtualFilePath,
                    Line = 0,
                    Column = 0,
                    Severity = RuleSeverity.Warning
                });
            }
        }

        return new AnalysisResult
        {
            SourcePath = virtualFilePath,
            AnalyzedAt = startTime,
            Duration = DateTime.UtcNow - startTime,
            Violations = violations,
            FilesAnalyzed = 1
        };
    }

    private async Task<IEnumerable<Violation>> AnalyzeFileInternalAsync(
        string filePath,
        IReadOnlyList<IRule> rules,
        CancellationToken cancellationToken)
    {
        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(code, path: filePath, cancellationToken: cancellationToken);
        var compilation = CreateCompilation(new[] { tree });
        var semanticModel = compilation.GetSemanticModel(tree);

        var context = new RuleContext
        {
            SyntaxTree = tree,
            SemanticModel = semanticModel,
            FilePath = filePath,
            Compilation = compilation
        };

        var violations = new List<Violation>();
        foreach (var rule in rules)
        {
            try
            {
                violations.AddRange(rule.Analyze(context));
            }
            catch (Exception ex)
            {
                violations.Add(new Violation
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Message = $"Erreur lors de l'exécution de la règle: {ex.Message}",
                    FilePath = filePath,
                    Line = 0,
                    Column = 0,
                    Severity = RuleSeverity.Warning
                });
            }
        }

        return violations;
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "CodengineAnalysis",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static List<string> GetFiles(EngineConfig config)
    {
        var basePath = Path.GetFullPath(config.SourcePath);
        if (!Directory.Exists(basePath))
            return new List<string>();

        var allFiles = new List<string>();

        foreach (var pattern in config.IncludePatterns)
        {
            var files = GetFilesMatchingPattern(basePath, pattern);
            allFiles.AddRange(files);
        }

        // Exclure les fichiers correspondant aux patterns d'exclusion
        var excludeMatchers = config.ExcludePatterns
            .Select(p => CreateMatcher(p))
            .ToList();

        return allFiles
            .Distinct()
            .Where(f => !excludeMatchers.Any(m => m(f)))
            .ToList();
    }

    private static IEnumerable<string> GetFilesMatchingPattern(string basePath, string pattern)
    {
        // Simplification: on supporte **/*.cs
        if (pattern.StartsWith("**/"))
        {
            var extension = pattern.Substring(3); // *.cs
            return Directory.GetFiles(basePath, extension, SearchOption.AllDirectories);
        }

        if (pattern.Contains("*"))
        {
            return Directory.GetFiles(basePath, pattern, SearchOption.TopDirectoryOnly);
        }

        var fullPath = Path.Combine(basePath, pattern);
        return File.Exists(fullPath) ? new[] { fullPath } : Array.Empty<string>();
    }

    private static Func<string, bool> CreateMatcher(string pattern)
    {
        // Simplification des patterns d'exclusion
        if (pattern.Contains("**"))
        {
            var segment = pattern.Replace("**", "").Replace("/", Path.DirectorySeparatorChar.ToString());
            return path => path.Contains(segment.Trim(Path.DirectorySeparatorChar));
        }

        return path => path.Contains(pattern);
    }
}

// Interface temporaire pour éviter la dépendance circulaire
public interface IRuleProvider
{
    IEnumerable<IRule> GetRules();
}

public interface IRule
{
    string Id { get; }
    string Name { get; }
    bool IsEnabled { get; }
    IEnumerable<Violation> Analyze(RuleContext context);
}
