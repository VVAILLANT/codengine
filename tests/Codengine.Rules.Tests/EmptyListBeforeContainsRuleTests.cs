using Codengine.Core.Models;
using Codengine.Rules.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Codengine.Rules.Tests;

public class EmptyListBeforeContainsRuleTests
{
    private readonly EmptyListBeforeContainsRule _rule = new();

    [Fact]
    public void Should_Detect_Missing_NullOrEmpty_Check()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        var query = GetItems().Where(x => ids.Contains(x.Id));
    }

    private IEnumerable<Item> GetItems() => new List<Item>();
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD002");
    }

    [Fact]
    public void Should_Not_Detect_When_Check_Exists()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        if (ids == null || !ids.Any()) return;
        var query = GetItems().Where(x => ids.Contains(x.Id));
    }

    private IEnumerable<Item> GetItems() => new List<Item>();
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Count_Check_Exists()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return;
        var query = GetItems().Where(x => ids.Contains(x.Id));
    }

    private IEnumerable<Item> GetItems() => new List<Item>();
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    private List<Violation> AnalyzeCode(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);

        var context = new RuleContext
        {
            SyntaxTree = tree,
            SemanticModel = null,
            FilePath = "test.cs",
            Compilation = null
        };

        return _rule.Analyze(context).ToList();
    }
}
