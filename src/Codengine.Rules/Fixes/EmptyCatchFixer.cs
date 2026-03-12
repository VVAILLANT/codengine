using Codengine.Core.Fixes;
using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.Fixes;

/// <summary>
/// Auto-fix pour COD005: ajoute un log ou rethrow dans les catch vides.
/// </summary>
public class EmptyCatchFixer : ICodeFixer
{
    public string RuleId => "COD005";
    public string Description => "Ajoute un rethrow dans les blocs catch vides";

    public bool CanFix(Violation violation) => violation.RuleId == RuleId;

    public Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken cancellationToken = default)
    {
        var root = tree.GetRoot(cancellationToken);

        // Trouver le catch clause
        var catchClause = root.DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .FirstOrDefault(c =>
            {
                var lineSpan = c.GetLocation().GetLineSpan();
                return lineSpan.StartLinePosition.Line + 1 == violation.Line;
            });

        if (catchClause == null)
            return Task.FromResult(FixResult.Failed("Impossible de localiser le bloc catch."));

        // Déterminer le nom de l'exception
        var exceptionName = catchClause.Declaration?.Identifier.Text ?? "ex";
        var hasDeclaration = catchClause.Declaration != null;

        // Si pas de déclaration, en ajouter une
        CatchDeclarationSyntax? newDeclaration = null;
        if (!hasDeclaration)
        {
            newDeclaration = SyntaxFactory.CatchDeclaration(
                SyntaxFactory.IdentifierName("Exception"),
                SyntaxFactory.Identifier("ex"));
            exceptionName = "ex";
        }

        // Créer le nouveau corps avec un commentaire TODO et un throw
        var throwStatement = SyntaxFactory.ParseStatement(
            $"// TODO: Gérer correctement l'exception ou la logger\n                throw; // Rethrow pour ne pas masquer l'erreur\n");

        var newBlock = SyntaxFactory.Block(throwStatement);

        // Créer le nouveau catch
        var newCatchClause = catchClause
            .WithDeclaration(newDeclaration ?? catchClause.Declaration)
            .WithBlock(newBlock);

        var newRoot = root.ReplaceNode(catchClause, newCatchClause);
        var newCode = newRoot.ToFullString();

        return Task.FromResult(FixResult.Succeeded(newCode, 2));
    }
}
