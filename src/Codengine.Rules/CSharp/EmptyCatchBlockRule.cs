using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les blocs catch vides qui avalent silencieusement les exceptions.
/// </summary>
public class EmptyCatchBlockRule : RuleBase
{
    public override string Id => "COD005";
    public override string Name => "EmptyCatchBlock";
    public override string Description =>
        "Les blocs catch vides masquent les erreurs et rendent le débogage difficile.";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "ErrorHandling";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var catchClauses = root.DescendantNodes()
            .OfType<CatchClauseSyntax>();

        foreach (var catchClause in catchClauses)
        {
            if (IsEmptyOrCommentOnly(catchClause.Block))
            {
                var exceptionType = catchClause.Declaration?.Type?.ToString() ?? "Exception";

                violations.Add(CreateViolation(
                    context,
                    catchClause,
                    $"Bloc catch vide pour '{exceptionType}'. Les exceptions ne doivent pas être ignorées silencieusement.",
                    "Ajouter un traitement ou au minimum un log de l'exception."));
            }
        }

        return violations;
    }

    private static bool IsEmptyOrCommentOnly(BlockSyntax? block)
    {
        if (block == null)
            return true;

        // Vérifier s'il y a des statements (hors commentaires)
        var statements = block.Statements;

        if (statements.Count == 0)
            return true;

        // Vérifier si tous les statements sont des commentaires ou des throw
        foreach (var statement in statements)
        {
            var text = statement.ToString().Trim();

            // Ignorer les commentaires
            if (text.StartsWith("//") || text.StartsWith("/*"))
                continue;

            // Un throw ou rethrow est acceptable
            if (statement is ThrowStatementSyntax)
                return false;

            // Tout autre statement rend le catch non-vide
            return false;
        }

        return true;
    }
}
