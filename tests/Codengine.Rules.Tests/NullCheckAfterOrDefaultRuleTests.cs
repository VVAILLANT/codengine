using System.Linq;
using Codengine.Core.Models;
using Codengine.Rules.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Codengine.Rules.Tests;

public class NullCheckAfterOrDefaultRuleTests
{
    private readonly NullCheckAfterOrDefaultRule _rule = new();

    [Fact]
    public void Should_Detect_Missing_NullCheck_After_SingleOrDefault()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        var length = item.Length; // Violation: pas de null check
    }
}";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public void Should_Not_Detect_When_NullCheck_Exists()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        if (item == null) return;
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Using_NullConditional()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        var length = item?.Length ?? 0;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_FirstOrDefault_As_Well()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.FirstOrDefault();
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Usage_Inside_NullCheck_In_Nested_Block()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        foreach (var x in list)
        {
            if (item != null)
            {
                var length = item.Length;
            }
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_When_Safe_And_Unsafe_Usages_Coexist()
    {
        // Usage sécurisée dans un foreach + usage non sécurisée en fin de méthode
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        foreach (var x in list)
        {
            if (item != null)
            {
                var length = item.Length;
            }
        }

        var unsafeLength = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Single(violations);
        Assert.Equal("COD001", violations[0].RuleId);
    }

    [Fact]
    public void Should_Not_Detect_When_Guard_Clause_Protects()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        if (item == null) return;
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Report_Violation_At_Usage_Line_Not_Declaration()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        var x = 42;
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Single(violations);
        // La violation doit pointer sur item.Length, pas sur SingleOrDefault
        Assert.Equal(12, violations[0].Line);
    }

    [Fact]
    public void Should_Not_Detect_When_Variable_Is_Reassigned()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        item = ""safe value"";
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Guard_Clause_In_Parent_Scope()
    {
        // Guard clause dans un bloc parent (try) protège les usages dans les blocs enfants
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        if (item == null) throw new System.Exception();

        foreach (var x in list)
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_Usage_In_Else_Without_Check()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (true)
        {
            var x = 1;
        }
        else
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Single(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Only_Passed_As_Argument()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        DoSomething(item);
    }

    private void DoSomething(string s) { }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_When_NullCheck_Combined_With_Or()
    {
        // if (item != null || true) → ne protège PAS (on entre toujours)
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method(bool flag)
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (item != null || flag)
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Single(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_NullCheck_Combined_With_And()
    {
        // if (item != null && x > 0) → protège (les deux doivent être vraies)
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (item != null && list.Count > 0)
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Guard_With_IsNullOrEmpty()
    {
        // string.IsNullOrEmpty(item) comme guard clause → protège
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        if (string.IsNullOrEmpty(item)) return;
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Wrapped_In_IsNullOrWhiteSpace_Negation()
    {
        // if (!string.IsNullOrWhiteSpace(item)) → protège le bloc
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (!string.IsNullOrWhiteSpace(item))
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_IsPattern_Not_Null()
    {
        // if (item is not null) → protège le bloc
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (item is not null)
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Guard_With_IsNull_Pattern()
    {
        // if (item is null) return; → protège après
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        if (item is null) return;
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Coalesce_At_Declaration()
    {
        // var item = list.SingleOrDefault() ?? fallback → item garanti non-null
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"") ?? ""default"";
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_CoalesceAssignment_Before_Usage()
    {
        // item ??= fallback → après, item est garanti non-null (réassignation)
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");
        item ??= ""safe"";
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_NullCheck_No_Spaces()
    {
        // item!=null sans espaces → AST gère correctement (pas de dépendance aux espaces)
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.SingleOrDefault(x => x == ""a"");

        if (item!=null)
        {
            var length = item.Length;
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_LastOrDefault_As_Well()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.LastOrDefault();
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public void Should_Detect_ElementAtOrDefault_As_Well()
    {
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.ElementAtOrDefault(5);
        var length = item.Length;
    }
}";

        var violations = AnalyzeCode(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public void Should_Not_Detect_When_Value_Type()
    {
        // int est un type valeur → FirstOrDefault() retourne 0, pas null → pas de violation
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<int> { 1, 2, 3 };
        var item = list.FirstOrDefault();
        var text = item.ToString();
    }
}";

        var violations = AnalyzeCodeWithSemanticModel(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Detect_When_Reference_Type_With_SemanticModel()
    {
        // string est un type référence → doit détecter
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<string> { ""a"", ""b"" };
        var item = list.FirstOrDefault();
        var length = item.Length;
    }
}";

        var violations = AnalyzeCodeWithSemanticModel(code);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COD001");
    }

    [Fact]
    public void Should_Not_Detect_When_Struct_Type()
    {
        // DateTime est un type valeur (struct) → pas de risque null
        var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Test
{
    public void Method()
    {
        var list = new List<DateTime> { DateTime.Now };
        var item = list.SingleOrDefault();
        var year = item.Year;
    }
}";

        var violations = AnalyzeCodeWithSemanticModel(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Usage_In_Else_Of_Null_Throw_Guard()
    {
        // Cas : if (var == null) { throw; } else { var.Prop → safe }
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class EnPrestataire { public string Code { get; set; } }

public class Test
{
    private string _code;

    public void Method(List<EnPrestataire> list)
    {
        var evenementsXmobile = list.FirstOrDefault(x => x.Code == ""XMobile"");
        if (evenementsXmobile == null)
        {
            throw new System.ArgumentException(""Aucun prestataire Xmobile trouvé"");
        }
        else
        {
            _code = evenementsXmobile.Code; // Safe : else garantit non-null
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_Same_Name_Reused_As_Foreach_Loop_Variable()
    {
        // Cas : evtPosition est déclaré via FirstOrDefault() dans un premier foreach (avec null check),
        // puis reréféré comme variable de boucle dans un second foreach distinct → faux positif COD001
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Item { public int Value { get; set; } }

public class Test
{
    public void Method(List<Item> list1, List<Item> list2)
    {
        foreach (var x in list1)
        {
            var evtPosition = list1.FirstOrDefault(s => s.Value == x.Value);
            if (evtPosition != null)
                System.Console.WriteLine(evtPosition.Value);
        }

        foreach (Item evtPosition in list2)
        {
            if (evtPosition.Value == 0) continue; // Ne doit PAS être signalé COD001
        }
    }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Should_Not_Detect_When_ConditionalAccess_Before_MemberAccess_In_Or_Chain()
    {
        // Cas : if (var?.X != value || var.Prop == null) return;
        // Quand var est null → var?.X = null → null != value → true → court-circuit → var.Prop jamais évalué
        var code = @"
using System.Linq;
using System.Collections.Generic;

public class Reseau { public string Code { get; set; } }
public class Direction { public Reseau Reseau { get; set; } public string AffreteArrivee { get; set; } }

public class Test
{
    public void Method(List<Direction> list)
    {
        var direction = list.FirstOrDefault(d => d.Reseau != null);
        if (direction?.Reseau?.Code?.StartsWith(""SC"") != true || direction.AffreteArrivee == null)
            return;
        System.Console.WriteLine(direction.AffreteArrivee);
    }
}";

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

    private List<Violation> AnalyzeCodeWithSemanticModel(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Ajouter la référence au runtime pour résoudre tous les types
        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeRef = MetadataReference.CreateFromFile(
            System.IO.Path.Combine(runtimeDir, "System.Runtime.dll"));

        var compilation = CSharpCompilation.Create(
            "TestAnalysis",
            new[] { tree },
            references.Append(runtimeRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);

        var context = new RuleContext
        {
            SyntaxTree = tree,
            SemanticModel = semanticModel,
            FilePath = "test.cs",
            Compilation = compilation
        };

        return _rule.Analyze(context).ToList();
    }
}
