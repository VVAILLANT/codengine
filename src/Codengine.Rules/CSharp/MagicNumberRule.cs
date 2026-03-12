using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les "magic numbers" - valeurs numériques littérales qui devraient être des constantes.
/// </summary>
public class MagicNumberRule : RuleBase
{
    public override string Id => "COD006";
    public override string Name => "MagicNumber";
    public override string Description =>
        "Les valeurs numériques littérales devraient être définies comme constantes nommées.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Maintainability";

    private static readonly HashSet<int> AllowedValues = new()
    {
        -1, 0, 1, 2, 10, 100, 1000
    };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var numericLiterals = root.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(IsNumericLiteral);

        foreach (var literal in numericLiterals)
        {
            if (ShouldIgnore(literal))
                continue;

            var value = literal.Token.ValueText;

            violations.Add(CreateViolation(
                context,
                literal,
                $"La valeur '{value}' devrait être définie comme une constante nommée.",
                $"private const int DESCRIPTIVE_NAME = {value};"));
        }

        return violations;
    }

    private static bool IsNumericLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() == SyntaxKind.NumericLiteralExpression;
    }

    private static bool ShouldIgnore(LiteralExpressionSyntax literal)
    {
        // Ignorer les valeurs autorisées
        if (literal.Token.Value is int intValue && AllowedValues.Contains(intValue))
            return true;

        // Ignorer dans les déclarations de constantes
        if (IsInConstantDeclaration(literal))
            return true;

        // Ignorer dans les attributs
        if (IsInAttribute(literal))
            return true;

        // Ignorer dans les enums
        if (IsInEnum(literal))
            return true;

        // Ignorer les index de tableaux
        if (IsArrayIndex(literal))
            return true;

        // Ignorer dans les tests unitaires (heuristique)
        if (IsInTestMethod(literal))
            return true;

        return false;
    }

    private static bool IsInConstantDeclaration(SyntaxNode node)
    {
        var field = node.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();
        if (field != null && field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            return true;

        var local = node.Ancestors().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
        if (local != null && local.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            return true;

        return false;
    }

    private static bool IsInAttribute(SyntaxNode node)
    {
        return node.Ancestors().OfType<AttributeSyntax>().Any();
    }

    private static bool IsInEnum(SyntaxNode node)
    {
        return node.Ancestors().OfType<EnumDeclarationSyntax>().Any();
    }

    private static bool IsArrayIndex(LiteralExpressionSyntax literal)
    {
        return literal.Parent is BracketedArgumentListSyntax or
               ArgumentSyntax { Parent: BracketedArgumentListSyntax };
    }

    private static bool IsInTestMethod(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null)
            return false;

        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "Test" or "Fact" or "Theory" or
                "TestMethod" or "TestCase" or "InlineData");
    }
}
