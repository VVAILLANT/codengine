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
