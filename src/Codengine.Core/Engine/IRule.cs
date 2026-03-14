using Codengine.Core.Models;

namespace Codengine.Core.Engine;

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
