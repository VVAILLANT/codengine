using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codengine.Rules.Abstractions;

public abstract class RuleBase : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract RuleSeverity Severity { get; }
    public virtual string Category => "General";
    public bool IsEnabled { get; set; } = true;

    public abstract IEnumerable<Violation> Analyze(RuleContext context);

    protected Violation CreateViolation(
        RuleContext context,
        SyntaxNode node,
        string message,
        string? suggestedFix = null)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;

        return new Violation
        {
            RuleId = Id,
            RuleName = Name,
            Message = message,
            FilePath = context.FilePath,
            Line = line,
            Column = column,
            Severity = Severity,
            CodeSnippet = node.ToString(),
            SuggestedFix = suggestedFix
        };
    }

    protected Violation CreateViolation(
        RuleContext context,
        SyntaxToken token,
        string message,
        string? suggestedFix = null)
    {
        var lineSpan = token.GetLocation().GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;

        return new Violation
        {
            RuleId = Id,
            RuleName = Name,
            Message = message,
            FilePath = context.FilePath,
            Line = line,
            Column = column,
            Severity = Severity,
            CodeSnippet = token.ToString(),
            SuggestedFix = suggestedFix
        };
    }
}
