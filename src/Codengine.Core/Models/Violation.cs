namespace Codengine.Core.Models;

public class Violation
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required RuleSeverity Severity { get; init; }
    public string? CodeSnippet { get; init; }
    public string? SuggestedFix { get; init; }

    public override string ToString()
    {
        return $"[{Severity}] {RuleId}: {Message} at {FilePath}:{Line}:{Column}";
    }
}
