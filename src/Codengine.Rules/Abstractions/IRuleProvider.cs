namespace Codengine.Rules.Abstractions;

public interface IRuleProvider
{
    IEnumerable<IRule> GetRules();
    IRule? GetRuleById(string id);
    IEnumerable<IRule> GetRulesByCategory(string category);
}
