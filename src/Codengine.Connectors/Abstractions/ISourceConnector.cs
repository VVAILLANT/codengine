namespace Codengine.Connectors.Abstractions;

public interface ISourceConnector
{
    string Name { get; }
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SourceFile>> ExtractSourcesAsync(CancellationToken cancellationToken = default);
}

public class SourceFile
{
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required string Type { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
