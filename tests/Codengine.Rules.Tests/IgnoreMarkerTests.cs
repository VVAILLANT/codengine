using Codengine.Core.Engine;
using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Xunit;

namespace Codengine.Rules.Tests;

public class IgnoreMarkerTests
{
    private static async Task<IReadOnlyList<Violation>> AnalyzeAsync(string code)
    {
        var provider = new DefaultRuleProvider();
        var engine = new RoslynAnalysisEngine(new TestRuleProviderAdapter(provider));
        var result = await engine.AnalyzeCodeAsync(code, "test.cs");
        return result.Violations;
    }

    [Fact]
    public async Task Ignore_All_Rules_On_Violation_Line_With_Bare_Marker()
    {
        // COD001 violation is reported at the usage site → marker must be on that line
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString()); // codengine-ignore
    }
}";
        var violations = await AnalyzeAsync(code);
        Assert.DoesNotContain(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public async Task Ignore_Specific_Rule_When_RuleId_Listed()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString()); // codengine-ignore COD001
    }
}";
        var violations = await AnalyzeAsync(code);
        Assert.DoesNotContain(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public async Task Ignore_Multiple_Rules_When_Listed()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString()); // codengine-ignore COD001, COD002
    }
}";
        var violations = await AnalyzeAsync(code);
        Assert.DoesNotContain(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public async Task Keep_Violation_When_Different_Rule_Listed()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString()); // codengine-ignore COD002
    }
}";
        var violations = await AnalyzeAsync(code);
        // COD001 must still appear — only COD002 is ignored
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public async Task No_Marker_Means_Violation_Reported()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString());
    }
}";
        var violations = await AnalyzeAsync(code);
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public async Task Marker_Is_Case_Insensitive()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> items)
    {
        var item = items.FirstOrDefault();
        Console.WriteLine(item.ToString()); // CODENGINE-IGNORE
    }
}";
        var violations = await AnalyzeAsync(code);
        Assert.DoesNotContain(violations, v => v.RuleId == "COD001");
    }
}

// Adapters to bridge Codengine.Rules.Abstractions interfaces with Codengine.Core.Engine interfaces
file class TestRuleProviderAdapter : Codengine.Core.Engine.IRuleProvider
{
    private readonly DefaultRuleProvider _provider;

    public TestRuleProviderAdapter(DefaultRuleProvider provider) => _provider = provider;

    public IEnumerable<Codengine.Core.Engine.IRule> GetRules() =>
        _provider.GetRules().Select(r => new TestRuleAdapter(r));
}

file class TestRuleAdapter : Codengine.Core.Engine.IRule
{
    private readonly Codengine.Rules.Abstractions.IRule _rule;

    public TestRuleAdapter(Codengine.Rules.Abstractions.IRule rule) => _rule = rule;

    public string Id => _rule.Id;
    public string Name => _rule.Name;
    public bool IsEnabled => _rule.IsEnabled;

    public IEnumerable<Violation> Analyze(RuleContext context) => _rule.Analyze(context);
}
