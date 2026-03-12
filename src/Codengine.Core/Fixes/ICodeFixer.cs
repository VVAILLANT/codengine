using Codengine.Core.Models;
using Microsoft.CodeAnalysis;

namespace Codengine.Core.Fixes;

public interface ICodeFixer
{
    string RuleId { get; }
    string Description { get; }
    bool CanFix(Violation violation);
    Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken cancellationToken = default);
}

public class FixResult
{
    public bool Success { get; init; }
    public string? NewCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int LinesChanged { get; init; }

    public static FixResult Succeeded(string newCode, int linesChanged = 1) => new()
    {
        Success = true,
        NewCode = newCode,
        LinesChanged = linesChanged
    };

    public static FixResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}
