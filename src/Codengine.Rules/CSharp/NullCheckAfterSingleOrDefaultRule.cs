using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie qu'après chaque SingleOrDefault() ou FirstOrDefault(),
/// le résultat est vérifié pour null avant utilisation.
/// </summary>
public class NullCheckAfterSingleOrDefaultRule : RuleBase
{
    public override string Id => "COD001";
    public override string Name => "NullCheckAfterSingleOrDefault";
    public override string Description =>
        "Le résultat de SingleOrDefault() ou FirstOrDefault() doit être vérifié pour null avant utilisation.";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "NullSafety";

    private static readonly string[] TargetMethods = { "SingleOrDefault", "FirstOrDefault" };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        // Trouver toutes les invocations de méthodes
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (!IsTargetMethod(invocation))
                continue;

            var variableDeclaration = GetVariableDeclaration(invocation);
            if (variableDeclaration == null)
                continue;

            var variableName = variableDeclaration.Identifier.Text;
            var containingBlock = GetContainingBlock(invocation);

            if (containingBlock == null)
                continue;

            if (!HasNullCheckBeforeUsage(containingBlock, variableName, invocation))
            {
                violations.Add(CreateViolation(
                    context,
                    invocation,
                    $"La variable '{variableName}' issue de {GetMethodName(invocation)}() doit être vérifiée pour null avant utilisation.",
                    $"Ajouter: if ({variableName} == null) return; // ou gérer le cas null"));
            }
        }

        return violations;
    }

    private static bool IsTargetMethod(InvocationExpressionSyntax invocation)
    {
        var methodName = GetMethodName(invocation);
        return TargetMethods.Contains(methodName);
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }

    private static VariableDeclaratorSyntax? GetVariableDeclaration(InvocationExpressionSyntax invocation)
    {
        var parent = invocation.Parent;

        while (parent != null)
        {
            if (parent is VariableDeclaratorSyntax declarator)
                return declarator;

            if (parent is LocalDeclarationStatementSyntax)
                break;

            if (parent is StatementSyntax)
                break;

            parent = parent.Parent;
        }

        // Chercher dans les ancêtres pour une déclaration de variable
        var equalsClause = invocation.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault();

        return equalsClause?.Parent as VariableDeclaratorSyntax;
    }

    private static SyntaxNode? GetContainingBlock(SyntaxNode node)
    {
        return node.Ancestors()
            .FirstOrDefault(n => n is BlockSyntax or MethodDeclarationSyntax or LocalFunctionStatementSyntax);
    }

    private static bool HasNullCheckBeforeUsage(SyntaxNode containingBlock, string variableName, InvocationExpressionSyntax declaration)
    {
        var statements = containingBlock.DescendantNodes()
            .OfType<StatementSyntax>()
            .ToList();

        var declarationIndex = statements.FindIndex(s => s.Contains(declaration));
        if (declarationIndex < 0)
            return true; // Si on ne trouve pas, on ne signale pas d'erreur

        // Vérifier les statements après la déclaration
        for (int i = declarationIndex + 1; i < statements.Count; i++)
        {
            var statement = statements[i];

            // Guard clause: if (var == null) return/throw → protège tout le reste
            if (IsNullGuardClause(statement, variableName))
                return true;

            // Vérifier si la variable est utilisée sans protection
            if (IsVariableUsedUnsafely(statement, variableName))
                return false;
        }

        return true; // Pas d'utilisation après = OK
    }

    private static bool IsNullGuardClause(StatementSyntax statement, string variableName)
    {
        if (statement is not IfStatementSyntax ifStatement)
            return false;

        var condition = ifStatement.Condition.ToString();

        // if (var == null) ou if (var is null) → guard clause potentielle
        bool checksForNull =
            condition.Contains($"{variableName} == null") ||
            condition.Contains($"{variableName} is null") ||
            condition.Contains($"null == {variableName}");

        if (!checksForNull)
            return false;

        // Le body doit contenir return ou throw pour être un guard clause
        var bodyText = ifStatement.Statement.ToString();
        return bodyText.Contains("return") || bodyText.Contains("throw");
    }

    private static bool IsVariableUsedUnsafely(StatementSyntax statement, string variableName)
    {
        // Si la variable est utilisée avec un accès membre direct (sans ?.)
        var identifiers = statement.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == variableName);

        foreach (var identifier in identifiers)
        {
            // Vérifier si c'est un accès membre direct
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression == identifier)
            {
                // Vérifier si ce n'est pas un accès conditionnel (?.)
                var fullExpression = memberAccess.ToString();
                if (!fullExpression.StartsWith($"{variableName}?."))
                {
                    // Vérifier si l'usage est protégé par un if (variable != null) ancêtre
                    if (IsInsideNullCheck(identifier, variableName))
                        continue;

                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideNullCheck(SyntaxNode node, string variableName)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is IfStatementSyntax ifStatement)
            {
                var condition = ifStatement.Condition.ToString();
                if (condition.Contains($"{variableName} != null") ||
                    condition.Contains($"{variableName} is not null") ||
                    condition.Contains($"null != {variableName}"))
                {
                    return true;
                }
            }
            current = current.Parent;
        }
        return false;
    }
}
