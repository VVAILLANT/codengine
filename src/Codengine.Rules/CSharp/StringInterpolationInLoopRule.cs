using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les concaténations de strings dans les boucles (performance).
/// </summary>
public class StringConcatenationInLoopRule : RuleBase
{
    public override string Id => "COD008";
    public override string Name => "StringConcatenationInLoop";
    public override string Description =>
        "Éviter la concaténation de strings dans les boucles. Utiliser StringBuilder.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Performance";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        // Trouver les boucles
        var loops = root.DescendantNodes()
            .Where(n => n is ForStatementSyntax or
                       ForEachStatementSyntax or
                       WhileStatementSyntax or
                       DoStatementSyntax);

        foreach (var loop in loops)
        {
            // Chercher les concaténations += avec des strings
            var assignments = loop.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(IsPlusEqualsStringAssignment);

            foreach (var assignment in assignments)
            {
                var variableName = assignment.Left.ToString();

                violations.Add(CreateViolation(
                    context,
                    assignment,
                    $"Concaténation de string '{variableName}' dans une boucle. Utiliser StringBuilder.",
                    $"var sb = new StringBuilder();\n// dans la boucle: sb.Append(...);\n// après: {variableName} = sb.ToString();"));
            }

            // Chercher aussi les binary expressions x = x + "..."
            var binaryConcat = loop.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(IsBinaryStringConcat);

            foreach (var concat in binaryConcat)
            {
                if (assignments.Any(a => a.Span.Contains(concat.Span)))
                    continue; // Déjà reporté

                violations.Add(CreateViolation(
                    context,
                    concat,
                    "Concaténation de string dans une boucle. Utiliser StringBuilder.",
                    "Utiliser StringBuilder.Append() pour de meilleures performances."));
            }
        }

        return violations;
    }

    private static bool IsPlusEqualsStringAssignment(AssignmentExpressionSyntax assignment)
    {
        if (assignment.OperatorToken.Text != "+=")
            return false;

        // Vérifier si le côté droit est une string
        var rightText = assignment.Right.ToString();
        return rightText.StartsWith("\"") ||
               rightText.StartsWith("$\"") ||
               rightText.StartsWith("@\"") ||
               assignment.Right is InterpolatedStringExpressionSyntax;
    }

    private static bool IsBinaryStringConcat(AssignmentExpressionSyntax assignment)
    {
        if (assignment.OperatorToken.Text != "=")
            return false;

        if (assignment.Right is not BinaryExpressionSyntax binary)
            return false;

        // x = x + "..." ou x = "..." + x
        var leftText = assignment.Left.ToString();
        var binaryLeftText = binary.Left.ToString();
        var binaryRightText = binary.Right.ToString();

        var hasStringLiteral = binaryLeftText.Contains("\"") || binaryRightText.Contains("\"");
        var referencesSameVar = binaryLeftText == leftText || binaryRightText == leftText;

        return binary.OperatorToken.Text == "+" && hasStringLiteral && referencesSameVar;
    }
}
