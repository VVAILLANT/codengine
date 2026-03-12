using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie que les méthodes async se terminent par "Async".
/// </summary>
public class AsyncMethodNamingRule : RuleBase
{
    public override string Id => "COD003";
    public override string Name => "AsyncMethodNaming";
    public override string Description =>
        "Les méthodes async doivent avoir un nom se terminant par 'Async'.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Naming";

    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Main",
        "Dispose",
        "DisposeAsync"
    };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(IsAsyncMethod);

        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text;

            if (ExcludedNames.Contains(methodName))
                continue;

            if (!methodName.EndsWith("Async", StringComparison.Ordinal))
            {
                violations.Add(CreateViolation(
                    context,
                    method.Identifier,
                    $"La méthode async '{methodName}' devrait se terminer par 'Async'.",
                    $"Renommer en '{methodName}Async'"));
            }
        }

        return violations;
    }

    private static bool IsAsyncMethod(MethodDeclarationSyntax method)
    {
        return method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
    }
}
