using Codengine.Core.Models;

namespace Codengine.Rules.Abstractions;

public interface IRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    RuleSeverity Severity { get; }
    string Category { get; }
    bool IsEnabled { get; set; }

    IEnumerable<Violation> Analyze(RuleContext context);
}
