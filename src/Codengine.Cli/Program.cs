using System.CommandLine;
using Codengine.Connectors.Oracle;
using Codengine.Core.Configuration;
using Codengine.Core.Engine;
using Codengine.Core.Fixes;
using Codengine.Reporters;
using Codengine.Rules.Abstractions;
using Codengine.Rules.Fixes;

namespace Codengine.Cli;

class Program
{
    private static readonly string Version =
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Codengine - Analyseur de code statique pour C# et PL/SQL");

        // Commande analyze
        rootCommand.AddCommand(CreateAnalyzeCommand());

        // Commande fix
        rootCommand.AddCommand(CreateFixCommand());

        // Commande extract-oracle
        rootCommand.AddCommand(CreateExtractOracleCommand());

        // Commande list-rules
        rootCommand.AddCommand(CreateListRulesCommand());

        // Commande init (créer fichier config)
        rootCommand.AddCommand(CreateInitCommand());

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateAnalyzeCommand()
    {
        var pathArgument = new Argument<string>(
            "path",
            getDefaultValue: () => ".",
            description: "Chemin vers le répertoire ou fichier à analyser");

        var outputOption = new Option<string?>(
            new[] { "-o", "--output" },
            "Chemin du fichier de rapport");

        var formatOption = new Option<string>(
            new[] { "-f", "--format" },
            getDefaultValue: () => "console",
            description: "Format de sortie (console, json, html)");

        var verboseOption = new Option<bool>(
            new[] { "-v", "--verbose" },
            "Afficher les suggestions de correction");

        var disableRulesOption = new Option<string[]>(
            new[] { "-d", "--disable" },
            "Règles à désactiver (ex: COD001,COD002)")
        { AllowMultipleArgumentsPerToken = true };

        var configOption = new Option<string?>(
            new[] { "-c", "--config" },
            "Chemin vers le fichier de configuration");

        var command = new Command("analyze", "Analyser le code source")
        {
            pathArgument,
            outputOption,
            formatOption,
            verboseOption,
            disableRulesOption,
            configOption
        };

        command.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var disabledRules = context.ParseResult.GetValueForOption(disableRulesOption) ?? Array.Empty<string>();
            var configPath = context.ParseResult.GetValueForOption(configOption);

            await RunAnalysisAsync(path, output, format, verbose, disabledRules, configPath);
        });

        return command;
    }

    private static Command CreateFixCommand()
    {
        var pathArgument = new Argument<string>(
            "path",
            getDefaultValue: () => ".",
            description: "Chemin vers le répertoire ou fichier à corriger");

        var dryRunOption = new Option<bool>(
            new[] { "--dry-run" },
            "Afficher les corrections sans les appliquer");

        var rulesOption = new Option<string[]>(
            new[] { "-r", "--rules" },
            "Règles à corriger (par défaut: toutes)")
        { AllowMultipleArgumentsPerToken = true };

        var command = new Command("fix", "Appliquer les corrections automatiques")
        {
            pathArgument,
            dryRunOption,
            rulesOption
        };

        command.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var rules = context.ParseResult.GetValueForOption(rulesOption) ?? Array.Empty<string>();

            await RunFixAsync(path, dryRun, rules);
        });

        return command;
    }

    private static Command CreateExtractOracleCommand()
    {
        var connectionStringOption = new Option<string>(
            new[] { "-c", "--connection" },
            "Chaîne de connexion Oracle")
        { IsRequired = true };

        var schemaOption = new Option<string?>(
            new[] { "-s", "--schema" },
            "Schéma Oracle (par défaut: utilisateur courant)");

        var outputOption = new Option<string>(
            new[] { "-o", "--output" },
            getDefaultValue: () => "./oracle_packages",
            description: "Répertoire de sortie");

        var includeOption = new Option<string[]>(
            new[] { "-i", "--include" },
            "Patterns d'inclusion (ex: PKG_*)");

        var excludeOption = new Option<string[]>(
            new[] { "-e", "--exclude" },
            "Patterns d'exclusion");

        var noBodiesOption = new Option<bool>(
            "--no-bodies",
            "Ne pas extraire les bodies des packages");

        var command = new Command("extract-oracle", "Extraire les packages PL/SQL d'Oracle")
        {
            connectionStringOption,
            schemaOption,
            outputOption,
            includeOption,
            excludeOption,
            noBodiesOption
        };

        command.SetHandler(async context =>
        {
            var connectionString = context.ParseResult.GetValueForOption(connectionStringOption)!;
            var schema = context.ParseResult.GetValueForOption(schemaOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var include = context.ParseResult.GetValueForOption(includeOption) ?? Array.Empty<string>();
            var exclude = context.ParseResult.GetValueForOption(excludeOption) ?? Array.Empty<string>();
            var noBodies = context.ParseResult.GetValueForOption(noBodiesOption);

            await ExtractOraclePackagesAsync(connectionString, schema, output, include, exclude, noBodies);
        });

        return command;
    }

    private static Command CreateListRulesCommand()
    {
        var command = new Command("list-rules", "Lister toutes les règles disponibles");

        command.SetHandler(() =>
        {
            var provider = new DefaultRuleProvider();
            var rules = provider.GetRules().ToList();

            Console.WriteLine();
            Console.WriteLine("Règles disponibles:");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            var categories = rules.GroupBy(r => r.Category).OrderBy(g => g.Key);

            foreach (var category in categories)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  [{category.Key}]");
                Console.ResetColor();

                foreach (var rule in category.OrderBy(r => r.Id))
                {
                    var severityColor = rule.Severity switch
                    {
                        Core.Models.RuleSeverity.Error => ConsoleColor.Red,
                        Core.Models.RuleSeverity.Warning => ConsoleColor.Yellow,
                        _ => ConsoleColor.Gray
                    };

                    Console.Write($"    {rule.Id} ");
                    Console.ForegroundColor = severityColor;
                    Console.Write($"[{rule.Severity}]");
                    Console.ResetColor();
                    Console.WriteLine($" {rule.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"         {rule.Description}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {rules.Count} règle(s)");
        });

        return command;
    }

    private static Command CreateInitCommand()
    {
        var command = new Command("init", "Créer un fichier de configuration codengine.config.json");

        command.SetHandler(async () =>
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "codengine.config.json");

            if (File.Exists(configPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Le fichier {configPath} existe déjà.");
                Console.ResetColor();
                return;
            }

            var config = @"{
  ""sourcePath"": ""."",
  ""includePatterns"": [""**/*.cs""],
  ""excludePatterns"": [
    ""**/bin/**"",
    ""**/obj/**"",
    ""**/node_modules/**"",
    ""**/Migrations/**"",
    ""**/*.Designer.cs""
  ],
  ""rules"": {
    ""COD001"": { ""enabled"": true },
    ""COD002"": { ""enabled"": true },
    ""COD003"": { ""enabled"": true },
    ""COD004"": { ""enabled"": true },
    ""COD005"": { ""enabled"": true },
    ""COD006"": { ""enabled"": false },
    ""COD007"": { ""enabled"": true },
    ""COD008"": { ""enabled"": true },
    ""COD009"": { ""enabled"": true }
  },
  ""reporting"": {
    ""format"": ""console"",
    ""verbose"": false,
    ""includeCodeSnippets"": true
  },
  ""failOnError"": true,
  ""failOnWarning"": false
}";

            await File.WriteAllTextAsync(configPath, config);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Fichier de configuration créé: {configPath}");
            Console.ResetColor();
        });

        return command;
    }

    private static async Task RunAnalysisAsync(
        string path,
        string? output,
        string format,
        bool verbose,
        string[] disabledRules,
        string? configPath)
    {
        PrintHeader();
        Console.WriteLine($"Analyse de: {Path.GetFullPath(path)}");
        Console.WriteLine();

        // Charger la configuration
        var fileConfig = await ConfigLoader.LoadAsync(configPath);

        var provider = new DefaultRuleProvider();

        // Appliquer les règles désactivées depuis le fichier de config
        if (fileConfig?.Rules != null)
        {
            foreach (var (ruleId, ruleConfig) in fileConfig.Rules)
            {
                var rule = provider.GetRuleById(ruleId);
                if (rule != null)
                {
                    rule.IsEnabled = ruleConfig.Enabled;
                }
            }
        }

        // Appliquer les règles désactivées depuis la ligne de commande
        if (disabledRules.Length > 0)
        {
            var disabledSet = new HashSet<string>(
                disabledRules.SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var rule in provider.GetRules())
            {
                if (disabledSet.Contains(rule.Id))
                {
                    rule.IsEnabled = false;
                }
            }
        }

        var engine = new RoslynAnalysisEngine(new RuleProviderAdapter(provider));

        var config = fileConfig?.ToEngineConfig() ?? new EngineConfig();
        config.SourcePath = path;

        var result = await engine.AnalyzeAsync(config);

        IReporter reporter = format.ToLowerInvariant() switch
        {
            "json" => new JsonReporter(),
            "html" => new HtmlReporter(),
            _ => new ConsoleReporter()
        };

        var reporterOptions = new ReporterOptions
        {
            OutputPath = output,
            Verbose = verbose,
            IncludeCodeSnippets = fileConfig?.Reporting?.IncludeCodeSnippets ?? true
        };

        await reporter.ReportAsync(result, reporterOptions);

        Environment.ExitCode = result.HasErrors ? 1 : 0;
    }

    private static async Task RunFixAsync(string path, bool dryRun, string[] rules)
    {
        PrintHeader();
        Console.WriteLine($"Correction de: {Path.GetFullPath(path)}");
        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Mode dry-run: aucune modification ne sera effectuée.");
            Console.ResetColor();
        }
        Console.WriteLine();

        var provider = new DefaultRuleProvider();
        var engine = new RoslynAnalysisEngine(new RuleProviderAdapter(provider));

        var config = new EngineConfig
        {
            SourcePath = path,
            IncludePatterns = new List<string> { "**/*.cs" },
            ExcludePatterns = new List<string> { "**/bin/**", "**/obj/**" }
        };

        // Analyser d'abord
        Console.WriteLine("Analyse en cours...");
        var result = await engine.AnalyzeAsync(config);

        if (result.TotalViolations == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Aucune violation à corriger.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Trouvé {result.TotalViolations} violation(s).");
        Console.WriteLine();

        // Créer le moteur de fix
        var fixers = new ICodeFixer[]
        {
            new NullCheckFixer(),
            new EmptyCatchFixer(),
            new AsyncNamingFixer()
        };

        var fixEngine = new CodeFixerEngine(fixers);

        // Filtrer par règles si spécifié
        var violations = result.Violations.AsEnumerable();
        if (rules.Length > 0)
        {
            var ruleSet = new HashSet<string>(rules, StringComparer.OrdinalIgnoreCase);
            violations = violations.Where(v => ruleSet.Contains(v.RuleId));
        }

        // Filtrer seulement les violations avec un fixer
        var fixableViolations = violations.Where(v => fixEngine.HasFixer(v.RuleId)).ToList();

        if (fixableViolations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Aucune violation n'a de correction automatique disponible.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"{fixableViolations.Count} violation(s) peuvent être corrigées automatiquement.");

        if (dryRun)
        {
            Console.WriteLine();
            foreach (var v in fixableViolations)
            {
                Console.WriteLine($"  [{v.RuleId}] {v.FilePath}:{v.Line} - {v.Message}");
            }
            return;
        }

        Console.WriteLine("Application des corrections...");
        Console.WriteLine();

        var summaries = await fixEngine.FixAllAsync(new Core.Models.AnalysisResult
        {
            SourcePath = path,
            AnalyzedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Violations = fixableViolations,
            FilesAnalyzed = 0
        });

        var totalFixed = summaries.Sum(s => s.Fixed);
        var totalFailed = summaries.Sum(s => s.Failed);

        foreach (var summary in summaries.Where(s => s.Modified))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Corrigé: {summary.FilePath} ({summary.Fixed} correction(s))");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"Résumé: {totalFixed} correction(s) appliquée(s), {totalFailed} échec(s).");

        if (summaries.Any(s => s.Errors.Count > 0))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nErreurs:");
            foreach (var error in summaries.SelectMany(s => s.Errors))
            {
                Console.WriteLine($"  {error}");
            }
            Console.ResetColor();
        }
    }

    private static async Task ExtractOraclePackagesAsync(
        string connectionString,
        string? schema,
        string output,
        string[] include,
        string[] exclude,
        bool noBodies)
    {
        PrintHeader();
        Console.WriteLine("Extraction Oracle");
        Console.WriteLine();

        var config = new OraclePackageExtractorConfig
        {
            ConnectionString = connectionString,
            Schema = schema,
            OutputDirectory = output,
            IncludePackageBodies = !noBodies,
            IncludePatterns = include.ToList(),
            ExcludePatterns = exclude.ToList()
        };

        var extractor = new OraclePackageExtractor(config);

        Console.WriteLine("Test de connexion...");
        if (!await extractor.TestConnectionAsync())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Échec de la connexion à Oracle.");
            Console.ResetColor();
            Environment.ExitCode = 1;
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Connexion OK.");
        Console.ResetColor();
        Console.WriteLine("Extraction en cours...");
        Console.WriteLine();

        await extractor.ExtractAndSaveAsync();
    }

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Codengine v{Version}");
        Console.ResetColor();
    }
}

// Adapter pour faire le pont entre Core et Rules
internal class RuleProviderAdapter : Codengine.Core.Engine.IRuleProvider
{
    private readonly DefaultRuleProvider _provider;

    public RuleProviderAdapter(DefaultRuleProvider provider)
    {
        _provider = provider;
    }

    public IEnumerable<Codengine.Core.Engine.IRule> GetRules()
    {
        return _provider.GetRules().Select(r => new RuleAdapter(r));
    }
}

internal class RuleAdapter : Codengine.Core.Engine.IRule
{
    private readonly Codengine.Rules.Abstractions.IRule _rule;

    public RuleAdapter(Codengine.Rules.Abstractions.IRule rule)
    {
        _rule = rule;
    }

    public string Id => _rule.Id;
    public string Name => _rule.Name;
    public bool IsEnabled => _rule.IsEnabled;

    public IEnumerable<Codengine.Core.Models.Violation> Analyze(Codengine.Core.Models.RuleContext context)
    {
        return _rule.Analyze(context);
    }
}
