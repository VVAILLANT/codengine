using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie que les objets IDisposable sont utilisés dans un bloc using.
/// </summary>
public class DisposePatternRule : RuleBase
{
    public override string Id => "COD004";
    public override string Name => "DisposePattern";
    public override string Description =>
        "Les objets IDisposable doivent être utilisés dans un bloc using ou disposés explicitement.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Resources";

    private static readonly HashSet<string> KnownDisposableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SqlConnection", "OracleConnection", "DbConnection",
        "SqlCommand", "OracleCommand", "DbCommand",
        "SqlDataReader", "OracleDataReader", "DbDataReader",
        "StreamReader", "StreamWriter", "FileStream",
        "MemoryStream", "BinaryReader", "BinaryWriter",
        "HttpClient", "WebClient",
        "SqlTransaction", "DbTransaction",
        "Bitmap", "Graphics", "Font", "Brush", "Pen"
    };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        // Chercher les déclarations de variables avec new
        var variableDeclarations = root.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Where(IsNotInUsingStatement);

        foreach (var declaration in variableDeclarations)
        {
            foreach (var variable in declaration.Declaration.Variables)
            {
                if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation)
                    continue;

                var typeName = GetTypeName(creation.Type);
                if (!IsKnownDisposableType(typeName))
                    continue;

                var variableName = variable.Identifier.Text;
                var containingMethod = GetContainingMethod(declaration);

                if (containingMethod == null)
                    continue;

                if (!IsProperlyDisposed(containingMethod, variableName))
                {
                    violations.Add(CreateViolation(
                        context,
                        variable,
                        $"L'objet '{variableName}' de type '{typeName}' devrait être dans un bloc using.",
                        $"using var {variableName} = new {typeName}(...);"));
                }
            }
        }

        return violations;
    }

    private static bool IsNotInUsingStatement(LocalDeclarationStatementSyntax declaration)
    {
        // Vérifier si c'est une déclaration using
        if (declaration.UsingKeyword != default)
            return false;

        // Vérifier si c'est dans un bloc using
        var parent = declaration.Parent;
        while (parent != null)
        {
            if (parent is UsingStatementSyntax)
                return false;
            parent = parent.Parent;
        }

        return true;
    }

    private static string? GetTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax qualified => GetTypeName(qualified.Right),
            _ => null
        };
    }

    private static bool IsKnownDisposableType(string? typeName)
    {
        return typeName != null && KnownDisposableTypes.Contains(typeName);
    }

    private static MethodDeclarationSyntax? GetContainingMethod(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    }

    private static bool IsProperlyDisposed(MethodDeclarationSyntax method, string variableName)
    {
        var methodBody = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";

        // Vérifier les patterns de disposition
        return methodBody.Contains($"{variableName}.Dispose()") ||
               methodBody.Contains($"{variableName}?.Dispose()") ||
               methodBody.Contains($"using ({variableName})") ||
               methodBody.Contains($"using var {variableName}") ||
               methodBody.Contains($"await using") ||
               // Retour de la ressource (le caller est responsable)
               methodBody.Contains($"return {variableName}");
    }
}
