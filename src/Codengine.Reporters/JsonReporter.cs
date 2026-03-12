using System.Text.Json;
using System.Text.Json.Serialization;
using Codengine.Core.Models;

namespace Codengine.Reporters;

public class JsonReporter : IReporter
{
    public string Name => "json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task ReportAsync(AnalysisResult result, ReporterOptions options, CancellationToken cancellationToken = default)
    {
        var report = new JsonReport
        {
            Summary = new ReportSummary
            {
                SourcePath = result.SourcePath,
                AnalyzedAt = result.AnalyzedAt,
                DurationMs = (int)result.Duration.TotalMilliseconds,
                FilesAnalyzed = result.FilesAnalyzed,
                TotalViolations = result.TotalViolations,
                Criticals = result.Criticals,
                Errors = result.Errors,
                Warnings = result.Warnings,
                HasErrors = result.HasErrors
            },
            Violations = result.Violations.Select(v => new ViolationDto
            {
                RuleId = v.RuleId,
                RuleName = v.RuleName,
                Message = v.Message,
                FilePath = v.FilePath,
                Line = v.Line,
                Column = v.Column,
                Severity = v.Severity.ToString(),
                CodeSnippet = options.IncludeCodeSnippets ? v.CodeSnippet : null,
                SuggestedFix = v.SuggestedFix
            }).ToList()
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);

        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            await File.WriteAllTextAsync(options.OutputPath, json, cancellationToken);
            Console.WriteLine($"Rapport JSON généré: {options.OutputPath}");
        }
        else
        {
            Console.WriteLine(json);
        }
    }

    private class JsonReport
    {
        public required ReportSummary Summary { get; init; }
        public required List<ViolationDto> Violations { get; init; }
    }

    private class ReportSummary
    {
        public required string SourcePath { get; init; }
        public required DateTime AnalyzedAt { get; init; }
        public required int DurationMs { get; init; }
        public required int FilesAnalyzed { get; init; }
        public required int TotalViolations { get; init; }
        public required int Criticals { get; init; }
        public required int Errors { get; init; }
        public required int Warnings { get; init; }
        public required bool HasErrors { get; init; }
    }

    private class ViolationDto
    {
        public required string RuleId { get; init; }
        public required string RuleName { get; init; }
        public required string Message { get; init; }
        public required string FilePath { get; init; }
        public required int Line { get; init; }
        public required int Column { get; init; }
        public required string Severity { get; init; }
        public string? CodeSnippet { get; init; }
        public string? SuggestedFix { get; init; }
    }
}
