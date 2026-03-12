using System.Text;
using System.Web;
using Codengine.Core.Models;

namespace Codengine.Reporters;

public class HtmlReporter : IReporter
{
    public string Name => "html";

    public async Task ReportAsync(AnalysisResult result, ReporterOptions options, CancellationToken cancellationToken = default)
    {
        var html = GenerateHtml(result, options);

        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            await File.WriteAllTextAsync(options.OutputPath, html, cancellationToken);
            Console.WriteLine($"Rapport HTML généré: {options.OutputPath}");
        }
        else
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"codengine-report-{DateTime.Now:yyyyMMdd-HHmmss}.html");
            await File.WriteAllTextAsync(tempPath, html, cancellationToken);
            Console.WriteLine($"Rapport HTML généré: {tempPath}");
        }
    }

    private static string GenerateHtml(AnalysisResult result, ReporterOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"fr\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Codengine - Rapport d'analyse</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("    <header>");
        sb.AppendLine("        <h1>Codengine</h1>");
        sb.AppendLine("        <p class=\"subtitle\">Rapport d'analyse de code</p>");
        sb.AppendLine("    </header>");

        // Summary
        sb.AppendLine("    <main>");
        sb.AppendLine("        <section class=\"summary\">");
        sb.AppendLine("            <h2>Résumé</h2>");
        sb.AppendLine("            <div class=\"summary-grid\">");
        sb.AppendLine($"                <div class=\"summary-card\"><span class=\"label\">Source</span><span class=\"value\">{HttpUtility.HtmlEncode(result.SourcePath)}</span></div>");
        sb.AppendLine($"                <div class=\"summary-card\"><span class=\"label\">Fichiers analysés</span><span class=\"value\">{result.FilesAnalyzed}</span></div>");
        sb.AppendLine($"                <div class=\"summary-card\"><span class=\"label\">Durée</span><span class=\"value\">{result.Duration.TotalSeconds:F2}s</span></div>");
        sb.AppendLine($"                <div class=\"summary-card\"><span class=\"label\">Date</span><span class=\"value\">{result.AnalyzedAt:yyyy-MM-dd HH:mm}</span></div>");
        sb.AppendLine("            </div>");

        // Stats
        sb.AppendLine("            <div class=\"stats\">");
        sb.AppendLine($"                <div class=\"stat critical\"><span class=\"count\">{result.Criticals}</span><span class=\"type\">Critiques</span></div>");
        sb.AppendLine($"                <div class=\"stat error\"><span class=\"count\">{result.Errors}</span><span class=\"type\">Erreurs</span></div>");
        sb.AppendLine($"                <div class=\"stat warning\"><span class=\"count\">{result.Warnings}</span><span class=\"type\">Warnings</span></div>");
        sb.AppendLine($"                <div class=\"stat total\"><span class=\"count\">{result.TotalViolations}</span><span class=\"type\">Total</span></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </section>");

        // Violations by file
        if (result.Violations.Count > 0)
        {
            sb.AppendLine("        <section class=\"violations\">");
            sb.AppendLine("            <h2>Violations</h2>");

            var groupedByFile = result.Violations.GroupBy(v => v.FilePath);

            foreach (var fileGroup in groupedByFile)
            {
                sb.AppendLine($"            <div class=\"file-group\">");
                sb.AppendLine($"                <h3 class=\"file-path\">{HttpUtility.HtmlEncode(fileGroup.Key)}</h3>");
                sb.AppendLine("                <table>");
                sb.AppendLine("                    <thead><tr><th>Ligne</th><th>Règle</th><th>Sévérité</th><th>Message</th></tr></thead>");
                sb.AppendLine("                    <tbody>");

                foreach (var violation in fileGroup.OrderBy(v => v.Line))
                {
                    var severityClass = violation.Severity.ToString().ToLower();
                    sb.AppendLine($"                        <tr class=\"{severityClass}\">");
                    sb.AppendLine($"                            <td class=\"line\">{violation.Line}</td>");
                    sb.AppendLine($"                            <td class=\"rule\">{HttpUtility.HtmlEncode(violation.RuleId)}</td>");
                    sb.AppendLine($"                            <td class=\"severity\"><span class=\"badge {severityClass}\">{violation.Severity}</span></td>");
                    sb.AppendLine($"                            <td class=\"message\">{HttpUtility.HtmlEncode(violation.Message)}</td>");
                    sb.AppendLine("                        </tr>");

                    if (options.IncludeCodeSnippets && !string.IsNullOrEmpty(violation.CodeSnippet))
                    {
                        sb.AppendLine("                        <tr class=\"snippet-row\">");
                        sb.AppendLine($"                            <td colspan=\"4\"><pre class=\"snippet\">{HttpUtility.HtmlEncode(violation.CodeSnippet)}</pre></td>");
                        sb.AppendLine("                        </tr>");
                    }

                    if (options.Verbose && !string.IsNullOrEmpty(violation.SuggestedFix))
                    {
                        sb.AppendLine("                        <tr class=\"suggestion-row\">");
                        sb.AppendLine($"                            <td colspan=\"4\"><div class=\"suggestion\">Suggestion: {HttpUtility.HtmlEncode(violation.SuggestedFix)}</div></td>");
                        sb.AppendLine("                        </tr>");
                    }
                }

                sb.AppendLine("                    </tbody>");
                sb.AppendLine("                </table>");
                sb.AppendLine("            </div>");
            }

            sb.AppendLine("        </section>");
        }
        else
        {
            sb.AppendLine("        <section class=\"success\">");
            sb.AppendLine("            <div class=\"success-message\">Aucune violation détectée !</div>");
            sb.AppendLine("        </section>");
        }

        sb.AppendLine("    </main>");

        // Footer
        sb.AppendLine("    <footer>");
        sb.AppendLine("        <p>Généré par Codengine</p>");
        sb.AppendLine("    </footer>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GetCss()
    {
        return @"
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; line-height: 1.6; }
        header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 2rem; text-align: center; }
        header h1 { font-size: 2.5rem; margin-bottom: 0.5rem; }
        header .subtitle { opacity: 0.9; font-size: 1.1rem; }
        main { max-width: 1200px; margin: 2rem auto; padding: 0 1rem; }
        section { background: white; border-radius: 8px; padding: 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        h2 { color: #444; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 2px solid #eee; }
        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
        .summary-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
        .summary-card .label { display: block; font-size: 0.85rem; color: #666; }
        .summary-card .value { display: block; font-size: 1.2rem; font-weight: 600; color: #333; }
        .stats { display: flex; gap: 1rem; flex-wrap: wrap; }
        .stat { flex: 1; min-width: 120px; padding: 1.5rem; border-radius: 8px; text-align: center; }
        .stat .count { display: block; font-size: 2rem; font-weight: bold; }
        .stat .type { display: block; font-size: 0.9rem; opacity: 0.9; }
        .stat.critical { background: #dc3545; color: white; }
        .stat.error { background: #fd7e14; color: white; }
        .stat.warning { background: #ffc107; color: #333; }
        .stat.total { background: #6c757d; color: white; }
        .file-group { margin-bottom: 1.5rem; }
        .file-path { font-size: 1rem; color: #0066cc; padding: 0.5rem; background: #f0f7ff; border-radius: 4px; margin-bottom: 0.5rem; font-family: monospace; }
        table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
        th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
        th { background: #f8f9fa; font-weight: 600; }
        .line { width: 60px; font-family: monospace; color: #666; }
        .rule { width: 100px; font-family: monospace; }
        .severity { width: 100px; }
        .badge { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; }
        .badge.critical { background: #dc3545; color: white; }
        .badge.error { background: #fd7e14; color: white; }
        .badge.warning { background: #ffc107; color: #333; }
        .badge.info { background: #17a2b8; color: white; }
        .snippet-row td { padding: 0; }
        .snippet { background: #2d2d2d; color: #f8f8f2; padding: 1rem; margin: 0; border-radius: 0 0 4px 4px; overflow-x: auto; font-size: 0.85rem; }
        .suggestion { background: #d4edda; color: #155724; padding: 0.75rem; border-radius: 4px; font-size: 0.85rem; }
        .success { text-align: center; padding: 3rem; }
        .success-message { font-size: 1.5rem; color: #28a745; }
        footer { text-align: center; padding: 2rem; color: #666; font-size: 0.9rem; }
        @media (max-width: 768px) { .stats { flex-direction: column; } .stat { min-width: 100%; } }
        ";
    }
}
