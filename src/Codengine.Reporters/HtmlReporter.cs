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
        var filesWithViolations = result.Violations.Select(v => v.FilePath).Distinct().Count();

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
        sb.AppendLine($"            <div class=\"summary-card summary-card-full\"><span class=\"label\">Source</span><span class=\"value source-path\">{HttpUtility.HtmlEncode(result.SourcePath)}</span></div>");
        sb.AppendLine("            <div class=\"summary-grid\">");
        sb.AppendLine($"                <div class=\"summary-card\"><span class=\"label\">Fichiers analysés</span><span class=\"value\">{result.FilesAnalyzed}</span></div>");
        sb.AppendLine($"                <div class=\"summary-card summary-card-alert\"><span class=\"label\">Fichiers problématiques</span><span class=\"value\">{filesWithViolations}</span></div>");
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

        // Stats by rule
        if (result.Violations.Count > 0)
        {
            var byRule = result.Violations
                .GroupBy(v => new { v.RuleId, v.RuleName, v.Severity })
                .Select(g => new { g.Key.RuleId, g.Key.RuleName, g.Key.Severity, Count = g.Count() })
                .OrderBy(r => r.RuleId)
                .ToList();

            sb.AppendLine("            <div class=\"rules-breakdown\">");
            sb.AppendLine("                <h3>Par règle</h3>");
            sb.AppendLine("                <table class=\"rules-table\">");
            sb.AppendLine("                    <thead><tr><th>ID</th><th>Règle</th><th>Sévérité</th><th>Occurrences</th></tr></thead>");
            sb.AppendLine("                    <tbody>");
            foreach (var r in byRule)
            {
                var sc = r.Severity.ToString().ToLower();
                sb.AppendLine($"                        <tr>");
                sb.AppendLine($"                            <td class=\"rule\">{HttpUtility.HtmlEncode(r.RuleId)}</td>");
                sb.AppendLine($"                            <td>{HttpUtility.HtmlEncode(r.RuleName)}</td>");
                sb.AppendLine($"                            <td><span class=\"badge {sc}\">{r.Severity}</span></td>");
                sb.AppendLine($"                            <td class=\"rule-count\">{r.Count}</td>");
                sb.AppendLine($"                        </tr>");
            }
            sb.AppendLine("                    </tbody>");
            sb.AppendLine("                </table>");
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("        </section>");

        // Violations by file
        if (result.Violations.Count > 0)
        {
            sb.AppendLine("        <section class=\"violations\">");
            sb.AppendLine("            <h2>Violations</h2>");

            var groupedByFile = result.Violations
                .GroupBy(v => v.FilePath)
                .OrderBy(g => g.Key, Comparer<string>.Create(CompareFilePaths));
            var fileIndex = 0;

            foreach (var fileGroup in groupedByFile)
            {
                var criticals = fileGroup.Count(v => v.Severity == RuleSeverity.Critical);
                var errors    = fileGroup.Count(v => v.Severity == RuleSeverity.Error);
                var warnings  = fileGroup.Count(v => v.Severity == RuleSeverity.Warning);
                var detailsId = $"file-details-{fileIndex++}";

                sb.AppendLine($"            <div class=\"file-group\">");

                // En-tête cliquable
                sb.AppendLine($"                <div class=\"file-header\" onclick=\"toggleFile('{detailsId}', this)\">");
                sb.AppendLine($"                    <span class=\"file-path\">{HttpUtility.HtmlEncode(fileGroup.Key)}</span>");
                sb.AppendLine($"                    <button class=\"copy-btn\" data-copy=\"{HttpUtility.HtmlEncode(Path.GetFileName(fileGroup.Key))}\" title=\"Copier le nom du fichier\"><svg aria-hidden=\"true\" height=\"14\" width=\"14\" viewBox=\"0 0 16 16\"><path fill=\"currentColor\" d=\"M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z\"/><path fill=\"currentColor\" d=\"M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z\"/></svg></button>");
                sb.AppendLine($"                    <div class=\"file-badges\">");
                if (criticals > 0) sb.AppendLine($"                        <span class=\"badge critical\">{criticals}</span>");
                if (errors > 0)    sb.AppendLine($"                        <span class=\"badge error\">{errors}</span>");
                if (warnings > 0)  sb.AppendLine($"                        <span class=\"badge warning\">{warnings}</span>");
                sb.AppendLine($"                        <span class=\"toggle-icon\">▶</span>");
                sb.AppendLine($"                    </div>");
                sb.AppendLine($"                </div>");

                // Détails (masqués par défaut)
                sb.AppendLine($"                <div class=\"file-details\" id=\"{detailsId}\">");
                sb.AppendLine("                    <table>");
                sb.AppendLine("                        <thead><tr><th>Ligne</th><th>Règle</th><th>Sévérité</th><th>Message</th></tr></thead>");
                sb.AppendLine("                        <tbody>");

                foreach (var violation in fileGroup.OrderBy(v => v.Line))
                {
                    var severityClass = violation.Severity.ToString().ToLower();
                    sb.AppendLine($"                            <tr class=\"{severityClass}\">");
                    sb.AppendLine($"                                <td class=\"line\">{violation.Line}</td>");
                    sb.AppendLine($"                                <td class=\"rule\">{HttpUtility.HtmlEncode(violation.RuleId)}</td>");
                    sb.AppendLine($"                                <td class=\"severity\"><span class=\"badge {severityClass}\">{violation.Severity}</span></td>");
                    sb.AppendLine($"                                <td class=\"message\">{HttpUtility.HtmlEncode(violation.Message)}</td>");
                    sb.AppendLine("                            </tr>");

                    if (options.IncludeCodeSnippets && !string.IsNullOrEmpty(violation.CodeSnippet))
                    {
                        var firstLine = violation.CodeSnippet.Split('\n')[0].Trim();
                        sb.AppendLine("                            <tr class=\"snippet-row\">");
                        sb.AppendLine($"                                <td colspan=\"4\"><div class=\"snippet-wrapper\"><button class=\"copy-btn copy-btn-snippet\" data-copy=\"{HttpUtility.HtmlEncode(firstLine)}\" title=\"Copier la première ligne\"><svg aria-hidden=\"true\" height=\"14\" width=\"14\" viewBox=\"0 0 16 16\"><path fill=\"currentColor\" d=\"M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z\"/><path fill=\"currentColor\" d=\"M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z\"/></svg></button><pre class=\"snippet\">{HttpUtility.HtmlEncode(violation.CodeSnippet)}</pre></div></td>");
                        sb.AppendLine("                            </tr>");
                    }

                    if (options.Verbose && !string.IsNullOrEmpty(violation.SuggestedFix))
                    {
                        sb.AppendLine("                            <tr class=\"suggestion-row\">");
                        sb.AppendLine($"                                <td colspan=\"4\"><div class=\"suggestion\">Suggestion: {HttpUtility.HtmlEncode(violation.SuggestedFix)}</div></td>");
                        sb.AppendLine("                            </tr>");
                    }
                }

                sb.AppendLine("                        </tbody>");
                sb.AppendLine("                    </table>");
                sb.AppendLine("                </div>");
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

        sb.AppendLine("    <div id=\"toast\" class=\"toast\"></div>");
        sb.AppendLine("    <script>");
        sb.AppendLine("        function toggleFile(id, header) {");
        sb.AppendLine("            var el = document.getElementById(id);");
        sb.AppendLine("            var icon = header.querySelector('.toggle-icon');");
        sb.AppendLine("            var open = el.style.display !== 'none' && el.style.display !== '';");
        sb.AppendLine("            el.style.display = open ? 'none' : 'block';");
        sb.AppendLine("            icon.textContent = open ? '▶' : '▼';");
        sb.AppendLine("        }");
        sb.AppendLine("        function showToast(msg) {");
        sb.AppendLine("            var t = document.getElementById('toast');");
        sb.AppendLine("            t.textContent = msg;");
        sb.AppendLine("            t.className = 'toast show';");
        sb.AppendLine("            setTimeout(function() { t.className = 'toast'; }, 2000);");
        sb.AppendLine("        }");
        sb.AppendLine("        document.querySelectorAll('.copy-btn').forEach(function(btn) {");
        sb.AppendLine("            btn.addEventListener('click', function(e) {");
        sb.AppendLine("                e.stopPropagation();");
        sb.AppendLine("                e.preventDefault();");
        sb.AppendLine("                var text = btn.getAttribute('data-copy');");
        sb.AppendLine("                var ta = document.createElement('textarea');");
        sb.AppendLine("                ta.value = text;");
        sb.AppendLine("                ta.style.position = 'fixed';");
        sb.AppendLine("                ta.style.left = '-9999px';");
        sb.AppendLine("                document.body.appendChild(ta);");
        sb.AppendLine("                ta.focus();");
        sb.AppendLine("                ta.select();");
        sb.AppendLine("                document.execCommand('copy');");
        sb.AppendLine("                document.body.removeChild(ta);");
        sb.AppendLine("                btn.classList.add('copied');");
        sb.AppendLine("                setTimeout(function() { btn.classList.remove('copied'); }, 1500);");
        sb.AppendLine("                var short = text.length > 50 ? text.substring(0, 50) + '...' : text;");
        sb.AppendLine("                showToast('Copié : ' + short);");
        sb.AppendLine("            });");
        sb.AppendLine("        });");
        sb.AppendLine("    </script>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Comparateur de chemins de fichiers reproduisant l'ordre Visual Studio 2022 (Modifications Git) :
    /// - Les fichiers dans des sous-dossiers apparaissent avant les fichiers à la racine du même répertoire parent
    /// - À profondeur identique, tri alphabétique insensible à la casse
    /// Exemple : ...\Commande\File.cs avant ...\CommandeEDI\File.cs avant ...\File.cs
    /// </summary>
    private static int CompareFilePaths(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var sep = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var partsX = x.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var partsY = y.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        int minLen = Math.Min(partsX.Length, partsY.Length);

        for (int i = 0; i < minLen; i++)
        {
            int cmp = StringComparer.OrdinalIgnoreCase.Compare(partsX[i], partsY[i]);
            if (cmp == 0) continue;

            // Les segments divergent ici — déterminer si chacun pointe sur un sous-dossier ou un fichier
            bool xIsFile = i == partsX.Length - 1;
            bool yIsFile = i == partsY.Length - 1;

            // X est dans un sous-dossier, Y est un fichier à ce niveau → X en premier
            if (!xIsFile && yIsFile) return -1;
            // X est un fichier à ce niveau, Y est dans un sous-dossier → Y en premier
            if (xIsFile && !yIsFile) return 1;
            // Même type (deux sous-dossiers ou deux fichiers) → alphabétique
            return cmp;
        }

        // Préfixe commun : le chemin le moins profond en premier
        return partsX.Length.CompareTo(partsY.Length);
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
        .summary-card-full { background: #f8f9fa; padding: 1rem; border-radius: 6px; margin-bottom: 1rem; }
        .summary-card-full .label { display: block; font-size: 0.85rem; color: #666; margin-bottom: 0.25rem; }
        .source-path { display: block; font-family: monospace; font-size: 1rem; font-weight: 600; color: #333; word-break: break-all; white-space: pre-wrap; }
        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
        .summary-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
        .summary-card.summary-card-alert { background: #fff3cd; border-left: 4px solid #ffc107; }
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
        .rules-breakdown { margin-top: 1.5rem; }
        .rules-breakdown h3 { color: #444; margin-bottom: 0.75rem; font-size: 1rem; text-transform: uppercase; letter-spacing: 0.05em; }
        .rules-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
        .rules-table th, .rules-table td { padding: 0.5rem 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
        .rules-table th { background: #f8f9fa; font-weight: 600; }
        .rule-count { font-weight: 700; text-align: center; width: 100px; }
        .file-group { margin-bottom: 0.5rem; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden; }
        .file-header { display: flex; align-items: center; justify-content: space-between; padding: 0.75rem 1rem; background: #f0f7ff; cursor: pointer; user-select: none; gap: 1rem; }
        .file-header:hover { background: #ddeeff; }
        .file-header .file-path { font-family: monospace; font-size: 0.9rem; color: #0066cc; word-break: break-all; flex: 1; }
        .file-badges { display: flex; align-items: center; gap: 0.4rem; flex-shrink: 0; }
        .toggle-icon { font-size: 0.7rem; color: #666; margin-left: 0.25rem; }
        .file-details { display: none; }
        table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
        th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
        th { background: #f8f9fa; font-weight: 600; }
        .line { width: 60px; font-family: monospace; color: #666; }
        .rule { width: 100px; font-family: monospace; }
        .severity { width: 100px; }
        .badge { padding: 0.2rem 0.5rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; }
        .badge.critical { background: #dc3545; color: white; }
        .badge.error { background: #fd7e14; color: white; }
        .badge.warning { background: #ffc107; color: #333; }
        .badge.info { background: #17a2b8; color: white; }
        .copy-btn { background: transparent; border: 1px solid #ccc; border-radius: 4px; cursor: pointer; padding: 0.25rem; color: #666; line-height: 0; flex-shrink: 0; transition: background 0.15s, color 0.15s, border-color 0.15s; display: inline-flex; align-items: center; justify-content: center; }
        .copy-btn:hover { background: #e0e0e0; color: #333; border-color: #999; }
        .copy-btn.copied { background: #28a745; color: white; border-color: #28a745; }
        .snippet-row td { padding: 0; }
        .snippet-wrapper { position: relative; }
        .copy-btn-snippet { position: absolute; top: 0.4rem; right: 0.4rem; border-color: #555; color: #999; }
        .copy-btn-snippet:hover { background: #444; color: white; border-color: #888; }
        .snippet { background: #2d2d2d; color: #f8f8f2; padding: 1rem; margin: 0; border-radius: 0 0 4px 4px; overflow-x: auto; font-size: 0.85rem; }
        .suggestion { background: #d4edda; color: #155724; padding: 0.75rem; border-radius: 4px; font-size: 0.85rem; }
        .success { text-align: center; padding: 3rem; }
        .success-message { font-size: 1.5rem; color: #28a745; }
        footer { text-align: center; padding: 2rem; color: #666; font-size: 0.9rem; }
        .toast { position: fixed; bottom: 2rem; left: 50%; transform: translateX(-50%) translateY(100px); background: #333; color: white; padding: 0.75rem 1.5rem; border-radius: 8px; font-size: 0.9rem; z-index: 9999; opacity: 0; transition: opacity 0.3s, transform 0.3s; pointer-events: none; white-space: nowrap; max-width: 90vw; overflow: hidden; text-overflow: ellipsis; }
        .toast.show { opacity: 1; transform: translateX(-50%) translateY(0); }
        @media (max-width: 768px) { .stats { flex-direction: column; } .stat { min-width: 100%; } }
        ";
    }
}
