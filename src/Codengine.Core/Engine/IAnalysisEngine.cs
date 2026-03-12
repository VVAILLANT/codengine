using Codengine.Core.Configuration;
using Codengine.Core.Models;

namespace Codengine.Core.Engine;

public interface IAnalysisEngine
{
    Task<AnalysisResult> AnalyzeAsync(EngineConfig config, CancellationToken cancellationToken = default);
    Task<AnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<AnalysisResult> AnalyzeCodeAsync(string code, string virtualFilePath = "code.cs", CancellationToken cancellationToken = default);
}
