using Codengine.Core.Fixes;
using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.Fixes;

/// <summary>
/// Auto-fix pour COD001: ajoute un null check après SingleOrDefault/FirstOrDefault.
/// </summary>
public class NullCheckFixer : ICodeFixer
{
    public string RuleId => "COD001";
    public string Description => "Ajoute une vérification null après SingleOrDefault/FirstOrDefault";

    public bool CanFix(Violation violation) => violation.RuleId == RuleId;

    public Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken cancellationToken = default)
    {
        var root = tree.GetRoot(cancellationToken);

        // Trouver le statement à la ligne de la violation
        var node = root.DescendantNodes()
            .FirstOrDefault(n =>
            {
                var lineSpan = n.GetLocation().GetLineSpan();
                return lineSpan.StartLinePosition.Line + 1 == violation.Line;
            });

        if (node == null)
            return Task.FromResult(FixResult.Failed("Impossible de localiser le code à corriger."));

        // Trouver la déclaration de variable
        var variableDeclarator = node.AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        if (variableDeclarator == null)
            return Task.FromResult(FixResult.Failed("Impossible de trouver la déclaration de variable."));

        var variableName = variableDeclarator.Identifier.Text;

        // Trouver le statement parent
        var statement = variableDeclarator.Ancestors()
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault();

        if (statement == null)
            return Task.FromResult(FixResult.Failed("Impossible de trouver le statement parent."));

        // Créer le null check
        var nullCheck = SyntaxFactory.ParseStatement($"if ({variableName} == null) throw new InvalidOperationException(\"La valeur attendue est nulle.\");\n");

        // Trouver le bloc parent et insérer le null check après le statement
        var block = statement.Parent as BlockSyntax;
        if (block == null)
            return Task.FromResult(FixResult.Failed("Le code doit être dans un bloc pour appliquer le fix."));

        var statementIndex = block.Statements.IndexOf(statement);
        var newStatements = block.Statements.Insert(statementIndex + 1, nullCheck);
        var newBlock = block.WithStatements(newStatements);

        var newRoot = root.ReplaceNode(block, newBlock);
        var newCode = newRoot.ToFullString();

        return Task.FromResult(FixResult.Succeeded(newCode, 1));
    }
}
