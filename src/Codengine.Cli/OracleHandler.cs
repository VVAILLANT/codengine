using System.Text;
using Codengine.Connectors.Oracle;
using Codengine.Core.Configuration;

namespace Codengine.Cli;

internal static class OracleHandler
{
    public static async Task RunAsync(
        string? connectionString,
        string? schema,
        string? output,
        string[] include,
        string[] exclude,
        bool noBodies,
        bool useConfig = false)
    {
        Program.PrintHeader();
        Console.WriteLine("Extraction Oracle");
        Console.WriteLine();

        // Charger la configuration uniquement si --config est passé
        OracleConfig? oracleConfig = null;
        if (useConfig)
        {
            try
            {
                var fileConfig = await ConfigLoader.LoadAsync();
                oracleConfig = fileConfig?.Oracle;
            }
            catch (InvalidOperationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }
        }

        if (oracleConfig != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Configuration chargée depuis codengine.config.json");
            Console.ResetColor();
        }

        // Fusionner : les arguments CLI ont priorité sur la configuration
        var effectiveConnectionString = connectionString ?? oracleConfig?.ConnectionString;
        var effectiveSchema = schema ?? oracleConfig?.Schema;
        var effectiveOutput = output ?? oracleConfig?.OutputDirectory ?? "./oracle_packages";
        var effectiveIncludeBodies = !noBodies && (oracleConfig?.IncludePackageBodies ?? true);
        var effectiveInclude = include.Length > 0 ? include.ToList() : oracleConfig?.IncludePatterns ?? new List<string>();
        var effectiveExclude = exclude.Length > 0 ? exclude.ToList() : oracleConfig?.ExcludePatterns ?? new List<string>();
        var effectiveEncoding = ResolveEncoding(oracleConfig?.Encoding);

        if (string.IsNullOrWhiteSpace(effectiveConnectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Erreur : la chaîne de connexion est requise.");
            Console.WriteLine("Spécifiez-la via --connection ou dans la section 'oracle' de codengine.config.json.");
            Console.ResetColor();
            Environment.ExitCode = 1;
            return;
        }

        var config = new OraclePackageExtractorConfig
        {
            ConnectionString = effectiveConnectionString,
            Schema = effectiveSchema,
            OutputDirectory = effectiveOutput,
            IncludePackageBodies = effectiveIncludeBodies,
            IncludePatterns = effectiveInclude,
            ExcludePatterns = effectiveExclude,
            Encoding = effectiveEncoding
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

    private static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Encodage '{encodingName}' non reconnu, utilisation de UTF-8 par défaut.");
            Console.ResetColor();
            return Encoding.UTF8;
        }
    }
}
