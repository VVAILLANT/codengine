using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie qu'après chaque SingleOrDefault() ou FirstOrDefault(),
/// le résultat est vérifié pour null avant utilisation.
/// Fonctionne comme un mini-analyseur de flux : chaque usage de la variable
/// est vérifié individuellement en remontant l'arbre syntaxique.
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

        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsTargetMethod);

        foreach (var invocation in invocations)
        {
            var variableDeclaration = GetVariableDeclaration(invocation);
            if (variableDeclaration == null)
                continue;

            var variableName = variableDeclaration.Identifier.Text;

            // Scope de recherche : méthode, constructeur, accesseur ou fonction locale
            var scope = invocation.Ancestors()
                .FirstOrDefault(n => n is BaseMethodDeclarationSyntax
                                  or AccessorDeclarationSyntax
                                  or LocalFunctionStatementSyntax);

            if (scope == null)
                continue;

            var firstUnsafe = FindFirstUnsafeUsage(scope, variableName, invocation);
            if (firstUnsafe != null)
            {
                violations.Add(CreateViolation(
                    context,
                    firstUnsafe,
                    $"La variable '{variableName}' issue de {GetMethodName(invocation)}() doit être vérifiée pour null avant utilisation.",
                    $"Ajouter: if ({variableName} == null) return; // ou gérer le cas null"));
            }
        }

        return violations;
    }

    /// <summary>
    /// Parcourt toutes les utilisations de la variable après la déclaration,
    /// dans l'ordre du code, et retourne la première utilisation non protégée.
    /// </summary>
    private static IdentifierNameSyntax? FindFirstUnsafeUsage(
        SyntaxNode scope,
        string variableName,
        InvocationExpressionSyntax declaration)
    {
        var declarationEnd = declaration.Span.End;

        var usages = scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == variableName && id.SpanStart > declarationEnd)
            .OrderBy(id => id.SpanStart);

        foreach (var usage in usages)
        {
            // Réassignation (item = ...) → la variable change de valeur, on arrête
            if (IsReassignment(usage))
                return null;

            // Seuls les accès membres directs (item.Foo) sont dangereux
            // item?.Foo → ConditionalAccessExpression (pas MemberAccess) → ignoré
            // item ?? x → BinaryExpression → ignoré
            // Foo(item) → Argument → ignoré
            if (usage.Parent is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression != usage)
                continue;

            // Protégé par un if (item != null) ancêtre ?
            if (IsInsideNullCheck(usage, variableName))
                continue;

            // Protégé par un guard clause (if item == null return/throw) avant ?
            if (HasGuardClauseBefore(usage, variableName))
                continue;

            // Première utilisation non protégée trouvée
            return usage;
        }

        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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

        var equalsClause = invocation.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault();

        return equalsClause?.Parent as VariableDeclaratorSyntax;
    }

    /// <summary>
    /// Vérifie si l'identifiant est une réassignation (item = newValue).
    /// </summary>
    private static bool IsReassignment(IdentifierNameSyntax usage)
    {
        return usage.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == usage;
    }

    /// <summary>
    /// Remonte l'arbre syntaxique pour vérifier si l'usage est à l'intérieur
    /// d'un if (variable != null) / if (variable is not null).
    /// </summary>
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

    /// <summary>
    /// Remonte les blocs parents pour vérifier si un guard clause
    /// (if variable == null return/throw) précède l'usage dans le même scope ou un scope parent.
    /// </summary>
    private static bool HasGuardClauseBefore(SyntaxNode usage, string variableName)
    {
        var current = usage;
        while (current != null)
        {
            if (current.Parent is BlockSyntax block)
            {
                foreach (var statement in block.Statements)
                {
                    // Ne regarder que les statements AVANT le nœud courant
                    if (statement.SpanStart >= current.SpanStart)
                        break;

                    if (IsNullGuardClause(statement, variableName))
                        return true;
                }
            }
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Vérifie si un statement est un guard clause : if (var == null) { return/throw; }
    /// </summary>
    private static bool IsNullGuardClause(StatementSyntax statement, string variableName)
    {
        if (statement is not IfStatementSyntax ifStatement)
            return false;

        var condition = ifStatement.Condition.ToString();

        bool checksForNull =
            condition.Contains($"{variableName} == null") ||
            condition.Contains($"{variableName} is null") ||
            condition.Contains($"null == {variableName}");

        if (!checksForNull)
            return false;

        var bodyText = ifStatement.Statement.ToString();
        return bodyText.Contains("return") || bodyText.Contains("throw");
    }
}
