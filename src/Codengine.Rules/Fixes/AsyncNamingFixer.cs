using Codengine.Core.Fixes;
using Codengine.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.Fixes;

/// <summary>
/// Auto-fix pour COD003: renomme les méthodes async pour terminer par "Async".
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

        // Renommer tous les usages dans le fichier
        var code = root.ToFullString();

        // Simple remplacement - pour un vrai refactoring il faudrait utiliser Roslyn Workspaces
        // On fait attention à ne pas remplacer des parties de mots
        var newCode = ReplaceMethodCalls(code, oldName, newName);

        return Task.FromResult(FixResult.Succeeded(newCode, 1));
    }

    private static string ReplaceMethodCalls(string code, string oldName, string newName)
    {
        // Patterns à remplacer pour les appels de méthode
        var patterns = new[]
        {
            ($" {oldName}(", $" {newName}("),
            ($".{oldName}(", $".{newName}("),
            ($">{oldName}(", $">{newName}("),
            ($"\t{oldName}(", $"\t{newName}("),
            ($"({oldName}(", $"({newName}("),
        };

        var result = code;
        foreach (var (old, @new) in patterns)
        {
            result = result.Replace(old, @new);
        }

        return result;
    }
}
