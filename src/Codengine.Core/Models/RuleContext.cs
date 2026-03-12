using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codengine.Core.Models;

public class RuleContext
{
    public required SyntaxTree SyntaxTree { get; init; }
    public required SemanticModel? SemanticModel { get; init; }
    public required string FilePath { get; init; }
    public required CSharpCompilation? Compilation { get; init; }
}
