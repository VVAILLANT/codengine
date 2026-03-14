namespace Codengine.Core.Models;

public class AnalysisResult
{
    public required string SourcePath { get; init; }
    public required DateTime AnalyzedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required IReadOnlyList<Violation> Violations { get; init; }
    public required int FilesAnalyzed { get; init; }

    private int? _errors;
    private int? _warnings;
    private int? _criticals;

    public int TotalViolations => Violations.Count;

    public int Errors => _errors ??= Violations.Count(v => v.Severity == RuleSeverity.Error);
    public int Warnings => _warnings ??= Violations.Count(v => v.Severity == RuleSeverity.Warning);
    public int Criticals => _criticals ??= Violations.Count(v => v.Severity == RuleSeverity.Critical);

    public bool HasErrors => Errors > 0 || Criticals > 0;
}
