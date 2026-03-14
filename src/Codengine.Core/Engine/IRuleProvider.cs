namespace Codengine.Core.Engine;

public interface IRuleProvider
{
    IEnumerable<IRule> GetRules();
    IRule? GetRuleById(string id);
    IEnumerable<IRule> GetRulesByCategory(string category);
}
