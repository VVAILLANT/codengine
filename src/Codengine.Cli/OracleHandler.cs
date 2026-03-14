using Codengine.Connectors.Oracle;

namespace Codengine.Cli;

internal static class OracleHandler
{
    public static async Task RunAsync(
        string connectionString,
        string? schema,
        string output,
        string[] include,
        string[] exclude,
        bool noBodies)
    {
        Program.PrintHeader();
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
}
