using Codengine.Core.Models;
using Codengine.Rules.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Codengine.Rules.Tests;

public class EmptyListBeforeContainsRuleTests
{
    private readonly EmptyListBeforeContainsRule _rule = new();

    [Fact]
    public void Should_Detect_Missing_Check_In_ORM_Query()
    {
        // Query<>().Where() sans vérification de la liste → doit détecter
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        var query = Query<Item>().Where(x => ids.Contains(x.Id));
    }

    private IQueryable<Item> Query<T>() => null;
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD002");
    }

    [Fact]
    public void Should_Not_Detect_When_Check_Exists_In_ORM_Query()
    {
        // Query<>().Where() avec vérification → ne doit pas détecter
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        if (ids == null || !ids.Any()) return;
        var query = Query<Item>().Where(x => ids.Contains(x.Id));
    }

    private IQueryable<Item> Query<T>() => null;
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Count_Check_Exists_In_ORM_Query()
    {
        // Query<>().Where() avec vérification Count → ne doit pas détecter
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return;
        var query = Query<Item>().Where(x => ids.Contains(x.Id));
    }

    private IQueryable<Item> Query<T>() => null;
}

public class Item { public int Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_Missing_Check_In_ORM_Constructor()
    {
        // new Query<T>(...).Where() sans vérification → doit détecter
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(List<long?> idsCommande)
    {
        var result = new Query<Item>(""connStr"")
            .Where(i => idsCommande.Contains(i.Id))
            .ToList();
    }
}

public class Query<T> {
    public Query(string conn) {}
    public Query<T> Where(System.Func<T,bool> f) => this;
    public System.Collections.Generic.List<T> ToList() => new();
}
public class Item { public long? Id { get; set; } }
";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD002");
    }

    [Fact]
    public void Should_Detect_In_Memory_Linq_Without_Check()
    {
        // Collection en mémoire (GetItems().Where) → doit détecter,
        // une liste vide dans Contains() annule le filtrage.
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

        Assert.Single(violations);
        Assert.Equal("COD002", violations[0].RuleId);
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
