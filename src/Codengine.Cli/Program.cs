using System.CommandLine;
using Codengine.Core.Models;
using Codengine.Rules.Abstractions;

namespace Codengine.Cli;

class Program
{
    private static readonly string Version =
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Codengine - Analyseur de code statique pour C# et PL/SQL");

        rootCommand.AddCommand(CreateAnalyzeCommand());
        rootCommand.AddCommand(CreateFixCommand());
        rootCommand.AddCommand(CreateExtractOracleCommand());
        rootCommand.AddCommand(CreateFormatOracleCommand());
        rootCommand.AddCommand(CreateListRulesCommand());
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

        var tagOption = new Option<bool>(
            "--tag",
            "Ajouter un commentaire // codengine[RULE] sur les lignes en violation");

        var untagOption = new Option<bool>(
            "--untag",
            "Retirer tous les commentaires codengine des fichiers source");

        var command = new Command("analyze", "Analyser le code source")
        {
            pathArgument,
            outputOption,
            formatOption,
            verboseOption,
            disableRulesOption,
            configOption,
            tagOption,
            untagOption
        };

        command.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var disabledRules = context.ParseResult.GetValueForOption(disableRulesOption) ?? Array.Empty<string>();
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var tag = context.ParseResult.GetValueForOption(tagOption);
            var untag = context.ParseResult.GetValueForOption(untagOption);

            await AnalyzeHandler.RunAsync(path, output, format, verbose, disabledRules, configPath, tag, untag);
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

            await FixHandler.RunAsync(path, dryRun, rules);
        });

        return command;
    }

    private static Command CreateExtractOracleCommand()
    {
        var connectionStringOption = new Option<string?>(
            new[] { "-c", "--connection" },
            "Chaîne de connexion Oracle");

        var schemaOption = new Option<string?>(
            new[] { "-s", "--schema" },
            "Schéma Oracle (par défaut: utilisateur courant)");

        var outputOption = new Option<string?>(
            new[] { "-o", "--output" },
            "Répertoire de sortie (par défaut: ./oracle_packages)");

        var includeOption = new Option<string[]>(
            new[] { "-i", "--include" },
            "Patterns d'inclusion (ex: PKG_*)");

        var excludeOption = new Option<string[]>(
            new[] { "-e", "--exclude" },
            "Patterns d'exclusion");

        var noBodiesOption = new Option<bool>(
            "--no-bodies",
            "Ne pas extraire les bodies des packages");

        var configOption = new Option<bool>(
            "--config",
            "Utiliser les valeurs par défaut de la section 'oracle' dans codengine.config.json");

        var command = new Command("extract-oracle", "Extraire les packages PL/SQL d'Oracle")
        {
            connectionStringOption,
            schemaOption,
            outputOption,
            includeOption,
            excludeOption,
            noBodiesOption,
            configOption
        };

        command.SetHandler(async context =>
        {
            var connectionString = context.ParseResult.GetValueForOption(connectionStringOption);
            var schema = context.ParseResult.GetValueForOption(schemaOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var include = context.ParseResult.GetValueForOption(includeOption) ?? Array.Empty<string>();
            var exclude = context.ParseResult.GetValueForOption(excludeOption) ?? Array.Empty<string>();
            var noBodies = context.ParseResult.GetValueForOption(noBodiesOption);
            var useConfig = context.ParseResult.GetValueForOption(configOption);

            await OracleHandler.RunAsync(connectionString, schema, output, include, exclude, noBodies, useConfig);
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
                        RuleSeverity.Error => ConsoleColor.Red,
                        RuleSeverity.Warning => ConsoleColor.Yellow,
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

    private static Command CreateFormatOracleCommand()
    {
        var pathArgument = new Argument<string?>(
            "path",
            getDefaultValue: () => null,
            description: "Répertoire contenant les fichiers .sql à formater");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Afficher les changements sans modifier les fichiers");

        var backupOption = new Option<bool>(
            "--backup",
            "Créer un fichier .bak avant modification");

        var indentSizeOption = new Option<int?>(
            "--indent-size",
            "Nombre d'espaces par niveau d'indentation");

        var uppercaseOption = new Option<bool?>(
            "--uppercase-keywords",
            "Mettre les mots-clés PL/SQL en majuscules");

        var engineOption = new Option<string?>(
            "--engine",
            "Moteur de formatage : auto, basic, sqlformatternet, combined, sqlcl (défaut: auto)");

        var configOption = new Option<bool>(
            "--config",
            "Utiliser les valeurs de la section 'oracle' dans codengine.config.json");

        var command = new Command("format-oracle", "Formater les fichiers PL/SQL Oracle")
        {
            pathArgument,
            dryRunOption,
            backupOption,
            indentSizeOption,
            uppercaseOption,
            engineOption,
            configOption
        };

        command.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var backup = context.ParseResult.GetValueForOption(backupOption);
            var indentSize = context.ParseResult.GetValueForOption(indentSizeOption);
            var uppercaseKeywords = context.ParseResult.GetValueForOption(uppercaseOption);
            var engine = context.ParseResult.GetValueForOption(engineOption);
            var useConfig = context.ParseResult.GetValueForOption(configOption);

            await FormatOracleHandler.RunAsync(path, dryRun, backup, indentSize, uppercaseKeywords, engine, useConfig);
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
  ""oracle"": {
    ""connectionString"": """",
    ""schema"": null,
    ""outputDirectory"": ""./oracle_packages"",
    ""includePackageBodies"": true,
    ""includePatterns"": [],
    ""excludePatterns"": [],
    ""sqlclPath"": null,
    ""format"": {
      ""indentSize"": 4,
      ""uppercaseKeywords"": true,
      ""maxConsecutiveBlankLines"": 1,
      ""trimTrailingWhitespace"": true,
      ""linesBetweenQueries"": 1,
      ""maxLineLength"": 0,
      ""engine"": ""auto""
    }
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

    internal static void PrintHeader()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Codengine v{Version}");
        Console.ResetColor();
    }
}
