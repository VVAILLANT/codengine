using Codengine.Core.Fixes;
using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.Fixes;

/// <summary>
/// Auto-fix pour COD003: renomme les méthodes async pour terminer par "Async".
/// Utilise l'AST Roslyn pour renommer la déclaration et tous les appels dans le fichier.
/// </summary>
public class AsyncNamingFixer : ICodeFixer
{
    public string RuleId => "COD003";
    public string Description => "Renomme les méthodes async pour terminer par 'Async'";

    public bool CanFix(Violation violation) => violation.RuleId == RuleId;

    public Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken cancellationToken = default)
    {
        var root = tree.GetRoot(cancellationToken);

        // Trouver la méthode à la ligne de la violation
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
            {
                var lineSpan = m.Identifier.GetLocation().GetLineSpan();
                return lineSpan.StartLinePosition.Line + 1 == violation.Line;
            });

        if (method == null)
            return Task.FromResult(FixResult.Failed("Impossible de localiser la méthode."));

        var oldName = method.Identifier.Text;
        var newName = oldName + "Async";

        // Collecter tous les noeuds à renommer via l'AST
        var rewriter = new AsyncRenameRewriter(oldName, newName);
        var newRoot = rewriter.Visit(root);

        return Task.FromResult(FixResult.Succeeded(newRoot.ToFullString(), rewriter.ReplacementCount));
    }

    private class AsyncRenameRewriter : CSharpSyntaxRewriter
    {
        private readonly string _oldName;
        private readonly string _newName;
        public int ReplacementCount { get; private set; }

        public AsyncRenameRewriter(string oldName, string newName)
        {
            _oldName = oldName;
            _newName = newName;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Identifier.Text == _oldName)
            {
                ReplacementCount++;
                node = node.WithIdentifier(
                    SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, _newName, node.Identifier.TrailingTrivia));
            }
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax id && id.Identifier.Text == _oldName)
            {
                ReplacementCount++;
                var newId = id.WithIdentifier(
                    SyntaxFactory.Identifier(id.Identifier.LeadingTrivia, _newName, id.Identifier.TrailingTrivia));
                node = node.WithExpression(newId);
            }
            else if (node.Expression is MemberAccessExpressionSyntax memberAccess
                     && memberAccess.Name.Identifier.Text == _oldName)
            {
                ReplacementCount++;
                var newName = memberAccess.Name.WithIdentifier(
                    SyntaxFactory.Identifier(memberAccess.Name.Identifier.LeadingTrivia, _newName, memberAccess.Name.Identifier.TrailingTrivia));
                node = node.WithExpression(memberAccess.WithName(newName));
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
