using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les ToList() inutiles dans les requêtes LINQ, notamment avant un Count() ou Any().
/// </summary>
public class ToListInQueryRule : RuleBase
{
    public override string Id => "COD009";
    public override string Name => "ToListInQuery";
    public override string Description =>
        "Éviter ToList() avant Count(), Any(), First(), etc. - cela matérialise inutilement la collection.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Performance";

    private static readonly HashSet<string> MethodsAfterToList = new()
    {
        "Count", "Any", "All", "First", "FirstOrDefault",
        "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Min", "Max", "Sum", "Average", "Contains"
    };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (!IsMethodAfterToList(invocation, out var toListInvocation, out var methodName))
                continue;

            violations.Add(CreateViolation(
                context,
                toListInvocation!,
                $"ToList() inutile avant {methodName}(). Cela matérialise toute la collection en mémoire.",
                $"Supprimer le .ToList() - {methodName}() fonctionne directement sur IEnumerable."));
        }

        return violations;
    }

    private static bool IsMethodAfterToList(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax? toListInvocation,
        out string? methodName)
    {
        toListInvocation = null;
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var currentMethodName = memberAccess.Name.Identifier.Text;
        if (!MethodsAfterToList.Contains(currentMethodName))
            return false;

        // Vérifier si l'expression précédente est un ToList()
        if (memberAccess.Expression is not InvocationExpressionSyntax previousInvocation)
            return false;

        if (previousInvocation.Expression is not MemberAccessExpressionSyntax previousMemberAccess)
            return false;

        var previousMethodName = previousMemberAccess.Name.Identifier.Text;
        if (previousMethodName is not ("ToList" or "ToArray"))
            return false;

        toListInvocation = previousInvocation;
        methodName = currentMethodName;
        return true;
    }
}
