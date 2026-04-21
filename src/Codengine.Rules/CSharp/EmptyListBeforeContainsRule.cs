using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie qu'avant d'utiliser liste.Contains() dans un .Where(),
/// la liste est vérifiée pour ne pas être nulle ou vide.
/// Une liste vide dans un Contains() peut annuler le filtrage et produire des résultats inattendus.
/// </summary>
public class EmptyListBeforeContainsRule : RuleBase
{
    public override string Id => "COD002";
    public override string Name => "EmptyListBeforeContains";
    public override string Description =>
        "Une liste utilisée dans Contains() au sein d'un .Where() doit être vérifiée pour null/vide avant utilisation. Une liste vide peut annuler le filtrage et produire des résultats inattendus.";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "NullSafety";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        // Trouver tous les appels Where()
        var whereInvocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsWhereMethod);

        foreach (var whereInvocation in whereInvocations)
        {
            // Chercher les appels Contains() dans le lambda du Where
            var containsInvocations = whereInvocation.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsContainsMethod);

            foreach (var containsInvocation in containsInvocations)
            {
                var listIdentifier = GetListIdentifier(containsInvocation);
                if (listIdentifier == null)
                    continue;

                var listName = listIdentifier.Identifier.Text;
                var containingMethod = GetContainingMethod(whereInvocation);

                if (containingMethod == null)
                    continue;

                // Si la liste est initialisée inline avec des éléments, elle ne peut pas être null/vide
                if (IsListInitializedWithElements(containingMethod, listName, whereInvocation))
                    continue;

                if (!HasNullOrEmptyCheckBefore(containingMethod, listName, whereInvocation))
                {
                    violations.Add(CreateViolation(
                        context,
                        containsInvocation,
                        $"La liste '{listName}' doit être vérifiée pour null/vide avant d'être utilisée dans Contains().",
                        $"Ajouter: if ({listName} == null || !{listName}.Any()) return; // ou gérer le cas"));
                }
            }
        }

        return violations;
    }

    private static bool IsWhereMethod(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text == "Where",
            _ => false
        };
    }

    private static bool IsContainsMethod(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text == "Contains",
            _ => false
        };
    }

    private static IdentifierNameSyntax? GetListIdentifier(InvocationExpressionSyntax containsInvocation)
    {
        // liste.Contains(x) -> on veut "liste"
        if (containsInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Expression as IdentifierNameSyntax;
        }
        return null;
    }

    private static MethodDeclarationSyntax? GetContainingMethod(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    }

    private static bool HasNullOrEmptyCheckBefore(
        MethodDeclarationSyntax method,
        string listName,
        InvocationExpressionSyntax whereInvocation)
    {
        var statements = method.Body?.Statements ??
            (method.ExpressionBody != null
                ? new SyntaxList<StatementSyntax>()
                : new SyntaxList<StatementSyntax>());

        var whereLineSpan = whereInvocation.GetLocation().GetLineSpan();
        var whereLine = whereLineSpan.StartLinePosition.Line;

        foreach (var statement in statements)
        {
            var statementLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;

            // Ne vérifier que les statements avant le Where
            if (statementLine >= whereLine)
                break;

            if (IsNullOrEmptyCheck(statement, listName))
                return true;
        }

        // Vérifier aussi les paramètres avec attributs comme [NotNull]
        // ou les guard clauses dans les appels de méthode
        var methodBody = method.Body?.ToString() ?? "";

        // Patterns courants de vérification
        if (methodBody.Contains($"{listName} == null") ||
            methodBody.Contains($"{listName} != null") ||
            methodBody.Contains($"{listName} is null") ||
            methodBody.Contains($"{listName} is not null") ||
            methodBody.Contains($"{listName}?.Any()") ||
            methodBody.Contains($"{listName}.Any()") ||
            methodBody.Contains($"!{listName}.Any()") ||
            methodBody.Contains($"{listName}.Count") ||
            methodBody.Contains($"{listName}?.Count") ||
            methodBody.Contains($"IsNullOrEmpty({listName})") ||
            methodBody.Contains($"Guard.") ||
            methodBody.Contains($"ArgumentNullException"))
        {
            // Vérifier que la vérification est AVANT l'utilisation
            var checkIndex = GetFirstCheckIndex(methodBody, listName);
            var useIndex = methodBody.IndexOf(whereInvocation.ToString());

            if (checkIndex >= 0 && checkIndex < useIndex)
                return true;
        }

        return false;
    }

    private static bool IsNullOrEmptyCheck(StatementSyntax statement, string listName)
    {
        var text = statement.ToString();

        // if (list == null || !list.Any()) return;
        // if (list?.Any() != true) return;
        // if (!list?.Any() ?? true) return;
        // Guard.Against.NullOrEmpty(list);

        if (statement is IfStatementSyntax ifStatement)
        {
            var condition = ifStatement.Condition.ToString();
            if ((condition.Contains(listName) && condition.Contains("null")) ||
                (condition.Contains(listName) && condition.Contains("Any")) ||
                (condition.Contains(listName) && condition.Contains("Count")))
            {
                return true;
            }
        }

        // Vérification via méthode Guard ou Assert
        if (text.Contains($"Guard") && text.Contains(listName))
            return true;

        return false;
    }

    /// <summary>
    /// Vérifie si la liste est déclarée et initialisée avec des éléments (collection initializer non vide)
    /// avant l'appel Where, ce qui rend impossible qu'elle soit null ou vide.
    /// </summary>
    private static bool IsListInitializedWithElements(
        MethodDeclarationSyntax method,
        string listName,
        InvocationExpressionSyntax whereInvocation)
    {
        var whereLine = whereInvocation.GetLocation().GetLineSpan().StartLinePosition.Line;

        // Chercher les déclarations de variables locales avant le Where
        var localDeclarations = method.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Where(d => d.GetLocation().GetLineSpan().StartLinePosition.Line < whereLine);

        foreach (var declaration in localDeclarations)
        {
            foreach (var variable in declaration.Declaration.Variables)
            {
                if (variable.Identifier.Text != listName)
                    continue;

                // Vérifier que l'initializer est une création d'objet avec collection initializer non vide
                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation
                    && objectCreation.Initializer is InitializerExpressionSyntax initializer
                    && initializer.Expressions.Count > 0)
                {
                    return true;
                }

                // Support pour new List<T> { ... } avec syntaxe implicite (target-typed new)
                if (variable.Initializer?.Value is ImplicitObjectCreationExpressionSyntax implicitCreation
                    && implicitCreation.Initializer is InitializerExpressionSyntax implicitInitializer
                    && implicitInitializer.Expressions.Count > 0)
                {
                    return true;
                }

                // Support pour collection expression [a, b, c]
                if (variable.Initializer?.Value is CollectionExpressionSyntax collectionExpr
                    && collectionExpr.Elements.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int GetFirstCheckIndex(string methodBody, string listName)
    {
        var patterns = new[]
        {
            $"{listName} == null",
            $"{listName} != null",
            $"{listName} is null",
            $"{listName} is not null",
            $"{listName}?.Any()",
            $"{listName}.Any()",
            $"{listName}.Count",
            $"IsNullOrEmpty({listName})"
        };

        var minIndex = int.MaxValue;
        foreach (var pattern in patterns)
        {
            var index = methodBody.IndexOf(pattern);
            if (index >= 0 && index < minIndex)
                minIndex = index;
        }

        return minIndex == int.MaxValue ? -1 : minIndex;
    }
}
