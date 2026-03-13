using Codengine.Core.Models;
using Codengine.Rules.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Codengine.Rules.Tests;

public class NullCheckAfterSingleOrDefaultRuleTests
{
    private readonly NullCheckAfterSingleOrDefaultRule _rule = new();

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
