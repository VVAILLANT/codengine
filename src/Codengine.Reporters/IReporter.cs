using Codengine.Core.Models;

namespace Codengine.Reporters;

public interface IReporter
{
    string Name { get; }
    Task ReportAsync(AnalysisResult result, ReporterOptions options, CancellationToken cancellationToken = default);
}

public class ReporterOptions
{
    public string? OutputPath { get; set; }
    public bool Verbose { get; set; }
    public bool IncludeCodeSnippets { get; set; } = true;
}
