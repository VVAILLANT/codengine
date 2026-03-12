using Codengine.Core.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Codengine.Core.Fixes;

public class CodeFixerEngine
{
    private readonly Dictionary<string, ICodeFixer> _fixers;

    public CodeFixerEngine(IEnumerable<ICodeFixer> fixers)
    {
        _fixers = fixers.ToDictionary(f => f.RuleId, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasFixer(string ruleId) => _fixers.ContainsKey(ruleId);

    public ICodeFixer? GetFixer(string ruleId) =>
        _fixers.TryGetValue(ruleId, out var fixer) ? fixer : null;

    public async Task<FixSummary> FixFileAsync(
        string filePath,
        IEnumerable<Violation> violations,
        CancellationToken cancellationToken = default)
    {
        var summary = new FixSummary { FilePath = filePath };

        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(code, path: filePath, cancellationToken: cancellationToken);
        var currentCode = code;

        // Trier par ligne décroissante pour éviter les décalages
        var sortedViolations = violations
            .Where(v => v.FilePath == filePath && HasFixer(v.RuleId))
            .OrderByDescending(v => v.Line);

        foreach (var violation in sortedViolations)
        {
            var fixer = GetFixer(violation.RuleId);
            if (fixer == null || !fixer.CanFix(violation))
            {
                summary.Skipped++;
                continue;
            }

            try
            {
                var result = await fixer.FixAsync(violation, tree, cancellationToken);

                if (result.Success && result.NewCode != null)
                {
                    currentCode = result.NewCode;
                    tree = CSharpSyntaxTree.ParseText(currentCode, path: filePath, cancellationToken: cancellationToken);
                    summary.Fixed++;
                    summary.LinesChanged += result.LinesChanged;
                }
                else
                {
                    summary.Failed++;
                    summary.Errors.Add($"[{violation.RuleId}] L{violation.Line}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                summary.Failed++;
                summary.Errors.Add($"[{violation.RuleId}] L{violation.Line}: {ex.Message}");
            }
        }

        if (summary.Fixed > 0)
        {
            await File.WriteAllTextAsync(filePath, currentCode, cancellationToken);
            summary.Modified = true;
        }

        return summary;
    }

    public async Task<List<FixSummary>> FixAllAsync(
        AnalysisResult analysisResult,
        CancellationToken cancellationToken = default)
    {
        var summaries = new List<FixSummary>();

        var violationsByFile = analysisResult.Violations
            .GroupBy(v => v.FilePath);

        foreach (var group in violationsByFile)
        {
            var summary = await FixFileAsync(group.Key, group, cancellationToken);
            summaries.Add(summary);
        }

        return summaries;
    }
}

public class FixSummary
{
    public required string FilePath { get; init; }
    public int Fixed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int LinesChanged { get; set; }
    public bool Modified { get; set; }
    public List<string> Errors { get; } = new();
}
