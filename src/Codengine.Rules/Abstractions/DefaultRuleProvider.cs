using System.Reflection;
using Codengine.Core.Engine;

namespace Codengine.Rules.Abstractions;

public class DefaultRuleProvider : IRuleProvider
{
    private readonly List<IRule> _rules;

    public DefaultRuleProvider()
    {
        _rules = DiscoverRules().ToList();
    }

    public DefaultRuleProvider(IEnumerable<IRule> rules)
    {
        _rules = rules.ToList();
    }

    public IEnumerable<IRule> GetRules() => _rules.Where(r => r.IsEnabled);

    public IRule? GetRuleById(string id) => _rules.FirstOrDefault(r => r.Id == id);

    public IEnumerable<IRule> GetRulesByCategory(string category) =>
        _rules.Where(r => r.Category == category && r.IsEnabled);

    private static IEnumerable<IRule> DiscoverRules()
    {
        var ruleType = typeof(IRule);
        var assembly = Assembly.GetExecutingAssembly();

        return assembly.GetTypes()
            .Where(t => ruleType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Id);
    }
}
