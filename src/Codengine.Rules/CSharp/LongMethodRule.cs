using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les méthodes trop longues qui devraient être refactorisées.
/// </summary>
public class LongMethodRule : RuleBase
{
    public override string Id => "COD007";
    public override string Name => "LongMethod";
    public override string Description =>
        "Les méthodes trop longues sont difficiles à maintenir et devraient être découpées.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Maintainability";

    private const int MaxLines = 50;
    private const int MaxStatements = 30;

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var lineCount = GetLineCount(method);
            var statementCount = GetStatementCount(method);

            if (lineCount > MaxLines)
            {
                violations.Add(CreateViolation(
                    context,
                    method.Identifier,
                    $"La méthode '{method.Identifier.Text}' fait {lineCount} lignes (max: {MaxLines}). Envisager de la découper.",
                    "Extraire des sous-méthodes pour améliorer la lisibilité."));
            }
            else if (statementCount > MaxStatements)
            {
                violations.Add(CreateViolation(
                    context,
                    method.Identifier,
                    $"La méthode '{method.Identifier.Text}' contient {statementCount} statements (max: {MaxStatements}).",
                    "Extraire des sous-méthodes pour améliorer la lisibilité."));
            }
        }

        return violations;
    }

    private static int GetLineCount(MethodDeclarationSyntax method)
    {
        var span = method.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    private static int GetStatementCount(MethodDeclarationSyntax method)
    {
        if (method.Body == null)
            return method.ExpressionBody != null ? 1 : 0;

        return method.Body.Statements.Count;
    }
}
