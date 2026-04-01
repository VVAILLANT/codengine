namespace Codengine.Connectors.Oracle;

public class PlSqlFormatterOptions
{
    /// <summary>
    /// Nombre d'espaces par niveau d'indentation.
    /// </summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>
    /// Mettre les mots-clés PL/SQL en majuscules.
    /// </summary>
    public bool UppercaseKeywords { get; init; } = true;

    /// <summary>
    /// Nombre maximal de lignes vides consécutives conservées.
    /// </summary>
    public int MaxConsecutiveBlankLines { get; init; } = 1;

    /// <summary>
    /// Supprimer les espaces en fin de ligne.
    /// </summary>
    public bool TrimTrailingWhitespace { get; init; } = true;
}
