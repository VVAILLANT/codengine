using Codengine.Connectors.Oracle.Formatting;

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

    /// <summary>
    /// Nombre de lignes vides entre les requêtes SQL (utilisé par SqlFormatterNet).
    /// </summary>
    public int LinesBetweenQueries { get; init; } = 1;

    /// <summary>
    /// Longueur maximale des colonnes avant retour à la ligne (utilisé par SqlFormatterNet, 0 = illimité).
    /// </summary>
    public int MaxLineLength { get; init; } = 0;

    /// <summary>
    /// Chemin vers l'exécutable Oracle SQLcl (optionnel, pour le moteur Sqlcl).
    /// </summary>
    public string? SqlclPath { get; init; }

    /// <summary>
    /// Moteur de formatage à utiliser. Auto sélectionne le meilleur moteur disponible.
    /// </summary>
    public FormattingEngineMode Engine { get; init; } = FormattingEngineMode.Auto;
}
