using System.Text;
using System.Text.RegularExpressions;

namespace Codengine.Connectors.Oracle;

/// <summary>
/// Formateur PL/SQL à états : ne modifie que l'indentation des lignes,
/// jamais le contenu du code. Respecte les string literals, commentaires
/// et identifiants quotés.
/// </summary>
public sealed partial class PlSqlFormatter
{
    private readonly PlSqlFormatterOptions _options;

    // Mots-clés PL/SQL reconnus pour la mise en majuscules
    private static readonly HashSet<string> PlSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "OR", "REPLACE", "PACKAGE", "BODY", "AS", "IS",
        "BEGIN", "END", "IF", "THEN", "ELSIF", "ELSE", "LOOP",
        "WHILE", "FOR", "EXIT", "WHEN", "CASE", "IN", "OUT",
        "NOCOPY", "RETURN", "RETURNS", "CURSOR", "OPEN", "FETCH",
        "CLOSE", "INTO", "BULK", "COLLECT", "LIMIT", "FORALL",
        "EXCEPTION", "RAISE", "RAISE_APPLICATION_ERROR",
        "DECLARE", "TYPE", "SUBTYPE", "RECORD", "TABLE", "INDEX",
        "BY", "OF", "REF", "ROWTYPE", "CONSTANT", "DEFAULT",
        "NOT", "NULL", "TRUE", "FALSE",
        "PROCEDURE", "FUNCTION", "PRAGMA", "AUTONOMOUS_TRANSACTION",
        "EXCEPTION_INIT", "RESTRICT_REFERENCES",
        "SELECT", "FROM", "WHERE", "AND", "SET", "UPDATE",
        "INSERT", "DELETE", "VALUES", "INTO", "COMMIT", "ROLLBACK",
        "SAVEPOINT", "MERGE", "USING", "MATCHED",
        "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "CROSS",
        "GROUP", "ORDER", "HAVING", "DISTINCT", "UNION", "ALL",
        "EXISTS", "BETWEEN", "LIKE", "ESCAPE", "WITH",
        "VARCHAR2", "NUMBER", "INTEGER", "PLS_INTEGER", "BINARY_INTEGER",
        "BOOLEAN", "DATE", "TIMESTAMP", "CLOB", "BLOB", "RAW",
        "CHAR", "NVARCHAR2", "NCHAR", "LONG",
        "EXECUTE", "IMMEDIATE", "DBMS_OUTPUT", "PUT_LINE",
        "OTHERS", "SQLERRM", "SQLCODE",
        "GRANT", "REVOKE", "TO", "PUBLIC", "SYNONYM",
        "NO_DATA_FOUND", "TOO_MANY_ROWS", "DUP_VAL_ON_INDEX"
    };

    public PlSqlFormatter(PlSqlFormatterOptions? options = null)
    {
        _options = options ?? new PlSqlFormatterOptions();
    }

    /// <summary>
    /// Formate le code PL/SQL en ne modifiant que l'indentation.
    /// Vérifie l'intégrité du contenu avant de retourner le résultat.
    /// </summary>
    public PlSqlFormatResult Format(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var lines = input.Split('\n');
        var formattedLines = new List<string>(lines.Length);

        // Pile de blocs : chaque entrée stocke le niveau d'indentation avant l'ouverture du bloc
        var blockStack = new Stack<BlockInfo>();
        int indentLevel = 0;
        bool inBlockComment = false;
        int consecutiveBlankLines = 0;

        // Alignement des paramètres sur la parenthèse ouvrante
        int parenAlignColumn = -1;
        int parenDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var trimmed = rawLine.Trim();

            // Gestion des lignes vides
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                consecutiveBlankLines++;
                if (consecutiveBlankLines <= _options.MaxConsecutiveBlankLines)
                {
                    formattedLines.Add("");
                }
                continue;
            }
            consecutiveBlankLines = 0;

            // Indentation courante : alignement paren ou bloc
            var currentIndent = parenDepth > 0 && parenAlignColumn >= 0
                ? new string(' ', parenAlignColumn)
                : Indent(indentLevel);

            // Si on est dans un commentaire bloc, on cherche la fin
            if (inBlockComment)
            {
                var endIdx = trimmed.IndexOf("*/", StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    inBlockComment = false;
                }
                formattedLines.Add(currentIndent + ApplyTrim(trimmed));
                continue;
            }

            // Commentaire ligne entière
            if (trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                formattedLines.Add(currentIndent + ApplyTrim(trimmed));
                continue;
            }

            // Début de commentaire bloc sans fin sur la même ligne
            if (trimmed.StartsWith("/*", StringComparison.Ordinal)
                && !trimmed.Contains("*/", StringComparison.Ordinal))
            {
                formattedLines.Add(currentIndent + ApplyTrim(trimmed));
                inBlockComment = true;
                continue;
            }

            // Extraire le contenu significatif (hors strings, commentaires)
            var significantText = StripNonCode(trimmed);
            var upper = significantText.ToUpperInvariant();

            // Ajuster l'indentation selon le type de ligne
            AdjustIndentation(upper, ref indentLevel, blockStack);

            // Recalculer l'indentation après l'ajustement de bloc
            currentIndent = parenDepth > 0 && parenAlignColumn >= 0
                ? new string(' ', parenAlignColumn)
                : Indent(indentLevel);

            // Formater la ligne
            var lineContent = _options.UppercaseKeywords
                ? UppercaseKeywordsInLine(trimmed)
                : trimmed;

            var formattedLine = currentIndent + ApplyTrim(lineContent);
            formattedLines.Add(formattedLine);

            // Augmenter l'indentation après avoir écrit la ligne si nécessaire
            AdjustIndentationAfter(upper, ref indentLevel, blockStack);

            // Suivi de l'alignement des parenthèses pour les listes de paramètres
            int netParens = CountNetParens(significantText);
            if (parenDepth == 0 && netParens > 0)
            {
                int col = FindOpenParenColumn(formattedLine);
                if (col >= 0)
                {
                    parenAlignColumn = col;
                }
                parenDepth = netParens;
            }
            else if (parenDepth > 0)
            {
                parenDepth += netParens;
                if (parenDepth <= 0)
                {
                    parenDepth = 0;
                    parenAlignColumn = -1;
                }
            }
        }

        var formatted = string.Join("\n", formattedLines);

        // Vérification d'intégrité : le contenu non-whitespace doit être identique
        // (case-insensitive quand UppercaseKeywords est activé)
        var originalContent = NormalizeContent(input);
        var formattedContent = NormalizeContent(formatted);

        var comparison = _options.UppercaseKeywords
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        bool integrityOk = string.Equals(originalContent, formattedContent, comparison);

        return new PlSqlFormatResult
        {
            FormattedCode = formatted,
            IsIntegrityValid = integrityOk,
            OriginalLineCount = lines.Length,
            FormattedLineCount = formattedLines.Count
        };
    }

    /// <summary>
    /// Ajuste l'indentation AVANT d'écrire la ligne courante (dedent).
    /// </summary>
    private static void AdjustIndentation(string upper, ref int indentLevel, Stack<BlockInfo> blockStack)
    {
        // END IF; / END LOOP; / END CASE; — ferme le bloc correspondant
        if (EndIfRegex().IsMatch(upper) || EndLoopRegex().IsMatch(upper) || EndCaseRegex().IsMatch(upper))
        {
            if (blockStack.Count > 0)
            {
                var block = blockStack.Pop();
                indentLevel = block.IndentLevel;
            }
            else
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }
            return;
        }

        // END; ou END nom; — ferme le bloc BEGIN/SubProgram/Package le plus proche,
        // en dépilant les blocs intermédiaires (WHEN, etc.)
        if (EndRegex().IsMatch(upper))
        {
            while (blockStack.Count > 0)
            {
                var block = blockStack.Pop();
                if (block.Type is BlockType.Begin or BlockType.SubProgram or BlockType.Package)
                {
                    indentLevel = block.IndentLevel;
                    return;
                }
            }
            indentLevel = 0;
            return;
        }

        // ELSIF / ELSE — revient au niveau du IF
        if (upper.StartsWith("ELSIF", StringComparison.Ordinal)
            || ElseStandaloneRegex().IsMatch(upper))
        {
            // Pop le bloc IF/ELSIF précédent
            if (blockStack.Count > 0)
            {
                var block = blockStack.Pop();
                indentLevel = block.IndentLevel;
            }
            else
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }
            return;
        }

        // EXCEPTION — revient au niveau du BEGIN parent
        if (upper.StartsWith("EXCEPTION", StringComparison.Ordinal))
        {
            // Pop les blocs internes jusqu'à trouver le BEGIN
            while (blockStack.Count > 0 && blockStack.Peek().Type != BlockType.Begin)
            {
                blockStack.Pop();
            }
            if (blockStack.Count > 0)
            {
                indentLevel = blockStack.Peek().IndentLevel;
            }
            else
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }
            return;
        }

        // WHEN ... THEN dans un bloc EXCEPTION — dedent le WHEN précédent s'il y en avait un
        if (WhenExceptionRegex().IsMatch(upper))
        {
            if (blockStack.Count > 0 && blockStack.Peek().Type == BlockType.When)
            {
                var block = blockStack.Pop();
                indentLevel = block.IndentLevel;
            }
            return;
        }
    }

    /// <summary>
    /// Ajuste l'indentation APRÈS avoir écrit la ligne courante (indent).
    /// </summary>
    private static void AdjustIndentationAfter(string upper, ref int indentLevel, Stack<BlockInfo> blockStack)
    {
        // END IF / END LOOP / END CASE — pas d'indent après
        if (EndIfRegex().IsMatch(upper) || EndLoopRegex().IsMatch(upper)
            || EndCaseRegex().IsMatch(upper))
            return;

        // END; / END nom; — après l'écriture, fermer aussi le SubProgram parent si présent
        // (en PL/SQL, un seul END; ferme à la fois le BEGIN et le PROCEDURE/FUNCTION IS/AS)
        if (EndRegex().IsMatch(upper))
        {
            if (blockStack.Count > 0 && blockStack.Peek().Type == BlockType.SubProgram)
            {
                var subBlock = blockStack.Pop();
                indentLevel = subBlock.IndentLevel;
            }
            return;
        }

        // BEGIN
        if (BeginRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.Begin, indentLevel));
            indentLevel++;
            return;
        }

        // IF ... THEN
        if (IfThenRegex().IsMatch(upper) && !EndIfRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.If, indentLevel));
            indentLevel++;
            return;
        }

        // ELSIF ... THEN (only when THEN is on the same line)
        if (upper.StartsWith("ELSIF", StringComparison.Ordinal) && upper.TrimEnd().EndsWith("THEN", StringComparison.Ordinal))
        {
            blockStack.Push(new BlockInfo(BlockType.If, indentLevel));
            indentLevel++;
            return;
        }

        // ELSIF without THEN (multi-line condition) — standalone THEN will open the block
        if (upper.StartsWith("ELSIF", StringComparison.Ordinal))
        {
            return;
        }

        // ELSE
        if (ElseStandaloneRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.If, indentLevel));
            indentLevel++;
            return;
        }

        // Standalone THEN (multi-line IF/ELSIF condition)
        if (ThenStandaloneRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.If, indentLevel));
            indentLevel++;
            return;
        }

        // LOOP (standalone or FOR...LOOP / WHILE...LOOP)
        if (LoopRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.Loop, indentLevel));
            indentLevel++;
            return;
        }

        // CASE statement
        if (CaseStatementRegex().IsMatch(upper) && !EndCaseRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.Case, indentLevel));
            indentLevel++;
            return;
        }

        // EXCEPTION
        if (upper.StartsWith("EXCEPTION", StringComparison.Ordinal))
        {
            // Ne pas push un nouveau bloc — on reste dans le BEGIN parent
            indentLevel++;
            return;
        }

        // WHEN dans exception
        if (WhenExceptionRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.When, indentLevel));
            indentLevel++;
            return;
        }

        // IS / AS après PROCEDURE, FUNCTION (single line)
        if (IsAsBlockRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.SubProgram, indentLevel));
            indentLevel++;
            return;
        }

        // ) IS / ) AS — multi-line PROCEDURE/FUNCTION parameter list
        if (CloseParenIsAsRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.SubProgram, indentLevel));
            indentLevel++;
            return;
        }

        // CREATE OR REPLACE PACKAGE [BODY] ... AS/IS
        if (CreatePackageRegex().IsMatch(upper))
        {
            blockStack.Push(new BlockInfo(BlockType.Package, indentLevel));
            indentLevel++;
            return;
        }
    }

    private enum BlockType
    {
        Begin,
        If,
        Loop,
        Case,
        When,
        SubProgram,
        Package
    }

    private sealed record BlockInfo(BlockType Type, int IndentLevel);

    /// <summary>
    /// Supprime les string literals, commentaires et identifiants quotés
    /// pour obtenir uniquement le code significatif (utilisé pour la détection de keywords).
    /// </summary>
    private static string StripNonCode(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool inString = false;
        bool inQuotedId = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            // Commentaire ligne
            if (!inString && !inQuotedId && c == '-' && i + 1 < line.Length && line[i + 1] == '-')
                break;

            // Commentaire bloc inline
            if (!inString && !inQuotedId && c == '/' && i + 1 < line.Length && line[i + 1] == '*')
            {
                var endIdx = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    i = endIdx + 1; // sauter le commentaire
                    sb.Append(' ');
                    continue;
                }
                break; // commentaire bloc ouvert, ignorer le reste
            }

            // String literal
            if (c == '\'' && !inQuotedId)
            {
                if (inString)
                {
                    // Vérifier l'échappement ''
                    if (i + 1 < line.Length && line[i + 1] == '\'')
                    {
                        i++; // sauter le quote échappé
                        continue;
                    }
                    inString = false;
                    continue;
                }
                inString = true;
                continue;
            }

            // Identifiant quoté "..."
            if (c == '"' && !inString)
            {
                inQuotedId = !inQuotedId;
                sb.Append(c);
                continue;
            }

            if (!inString && !inQuotedId)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Met en majuscules les mots-clés PL/SQL dans une ligne,
    /// en respectant les strings, commentaires et identifiants quotés.
    /// </summary>
    private static string UppercaseKeywordsInLine(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool inString = false;
        bool inQuotedId = false;
        bool inLineComment = false;
        int wordStart = -1;

        for (int i = 0; i <= line.Length; i++)
        {
            char c = i < line.Length ? line[i] : '\0';

            // Commentaire ligne
            if (!inString && !inQuotedId && !inLineComment
                && c == '-' && i + 1 < line.Length && line[i + 1] == '-')
            {
                FlushWord(line, sb, ref wordStart, i);
                sb.Append(line, i, line.Length - i);
                return sb.ToString();
            }

            // String literal
            if (c == '\'' && !inQuotedId && !inLineComment)
            {
                if (inString)
                {
                    if (i + 1 < line.Length && line[i + 1] == '\'')
                    {
                        FlushWord(line, sb, ref wordStart, i);
                        sb.Append("''");
                        i++;
                        continue;
                    }
                    inString = false;
                    FlushWord(line, sb, ref wordStart, i);
                    sb.Append(c);
                    continue;
                }
                FlushWord(line, sb, ref wordStart, i);
                inString = true;
                sb.Append(c);
                continue;
            }

            // Identifiant quoté
            if (c == '"' && !inString && !inLineComment)
            {
                FlushWord(line, sb, ref wordStart, i);
                inQuotedId = !inQuotedId;
                sb.Append(c);
                continue;
            }

            // Dans un string ou identifiant quoté, copier tel quel
            if (inString || inQuotedId || inLineComment)
            {
                FlushWord(line, sb, ref wordStart, i);
                if (i < line.Length) sb.Append(c);
                continue;
            }

            // Caractère de mot
            if (i < line.Length && (char.IsLetterOrDigit(c) || c == '_' || c == '$'))
            {
                if (wordStart < 0) wordStart = i;
            }
            else
            {
                FlushWord(line, sb, ref wordStart, i);
                if (i < line.Length) sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static void FlushWord(string line, StringBuilder sb, ref int wordStart, int currentPos)
    {
        if (wordStart < 0) return;

        var word = line.AsSpan(wordStart, currentPos - wordStart);
        if (PlSqlKeywords.Contains(word.ToString()))
        {
            sb.Append(word.ToString().ToUpperInvariant());
        }
        else
        {
            sb.Append(word);
        }
        wordStart = -1;
    }

    private string Indent(int level) =>
        level > 0 ? new string(' ', level * _options.IndentSize) : "";

    private string ApplyTrim(string line) =>
        _options.TrimTrailingWhitespace ? line.TrimEnd() : line;

    /// <summary>
    /// Normalise le contenu en supprimant tout le whitespace pour la vérification d'intégrité.
    /// </summary>
    private static string NormalizeContent(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compte le nombre net de parenthèses ouvertes dans le texte significatif (hors strings/commentaires).
    /// Retourne un nombre positif si plus de '(' que de ')'.
    /// </summary>
    private static int CountNetParens(string significantText)
    {
        int net = 0;
        foreach (char c in significantText)
        {
            if (c == '(') net++;
            else if (c == ')') net--;
        }
        return net;
    }

    /// <summary>
    /// Trouve la colonne d'alignement après la première parenthèse ouvrante non fermée
    /// dans la ligne formatée. Retourne -1 si aucune parenthèse éligible n'est trouvée
    /// (parenthèses équilibrées ou pas de contenu après la parenthèse).
    /// </summary>
    private static int FindOpenParenColumn(string line)
    {
        bool inString = false;
        int depth = 0;
        int lastOpenPos = -1;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\'' && !inString)
            {
                inString = true;
                continue;
            }

            if (c == '\'' && inString)
            {
                if (i + 1 < line.Length && line[i + 1] == '\'')
                {
                    i++; // quote échappé ''
                    continue;
                }
                inString = false;
                continue;
            }

            if (inString) continue;

            // Commentaire ligne — arrêter l'analyse
            if (c == '-' && i + 1 < line.Length && line[i + 1] == '-')
                break;

            if (c == '(')
            {
                depth++;
                if (depth == 1)
                {
                    lastOpenPos = i;
                }
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    lastOpenPos = -1; // cette ( était fermée, reset
                }
            }
        }

        // Vérifier qu'il y a du contenu non-vide après la parenthèse
        if (lastOpenPos >= 0 && lastOpenPos + 1 < line.Length)
        {
            var rest = line[(lastOpenPos + 1)..].TrimStart();
            if (rest.Length > 0 && !rest.StartsWith("--", StringComparison.Ordinal))
            {
                return lastOpenPos + 1;
            }
        }

        return -1;
    }

    // Regex compilées via GeneratedRegex pour les patterns de détection

    [GeneratedRegex(@"^END\s*(;|\s+\w+\s*;)", RegexOptions.IgnoreCase)]
    private static partial Regex EndRegex();

    [GeneratedRegex(@"^END\s+IF\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex EndIfRegex();

    [GeneratedRegex(@"^END\s+LOOP\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex EndLoopRegex();

    [GeneratedRegex(@"^END\s+CASE\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex EndCaseRegex();

    [GeneratedRegex(@"^ELSE\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ElseStandaloneRegex();

    [GeneratedRegex(@"^BEGIN\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex BeginRegex();

    [GeneratedRegex(@"IF\s+.+\s+THEN\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex IfThenRegex();

    [GeneratedRegex(@"(^|\s)LOOP\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LoopRegex();

    [GeneratedRegex(@"^CASE(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CaseStatementRegex();

    [GeneratedRegex(@"^WHEN\s+.+\s+THEN\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WhenExceptionRegex();

    [GeneratedRegex(@"^THEN\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ThenStandaloneRegex();

    [GeneratedRegex(@"(PROCEDURE|FUNCTION)\s+\w+.*\s+(IS|AS)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex IsAsBlockRegex();

    [GeneratedRegex(@"\)\s*(RETURN\s+.+\s+)?(IS|AS)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CloseParenIsAsRegex();

    [GeneratedRegex(@"^CREATE\s+(OR\s+REPLACE\s+)?PACKAGE(\s+BODY)?\s+\w+.*\s+(IS|AS)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CreatePackageRegex();
}

/// <summary>
/// Résultat du formatage PL/SQL avec métadonnées d'intégrité.
/// </summary>
public sealed class PlSqlFormatResult
{
    public required string FormattedCode { get; init; }
    public required bool IsIntegrityValid { get; init; }
    public required int OriginalLineCount { get; init; }
    public required int FormattedLineCount { get; init; }
}
