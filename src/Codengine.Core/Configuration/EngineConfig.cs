namespace Codengine.Core.Configuration;

public class EngineConfig
{
    public string SourcePath { get; set; } = ".";
    public List<string> IncludePatterns { get; set; } = new() { "**/*.cs" };
    public List<string> ExcludePatterns { get; set; } = new() { "**/bin/**", "**/obj/**", "**/node_modules/**" };
    public List<string> EnabledRuleIds { get; set; } = new();
    public List<string> DisabledRuleIds { get; set; } = new();
    public bool FailOnError { get; set; } = true;
    public bool FailOnWarning { get; set; } = false;
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
}
