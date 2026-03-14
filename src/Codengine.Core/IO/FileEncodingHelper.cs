using System.Text;

namespace Codengine.Core.IO;

/// <summary>
/// Lit un fichier texte en préservant exactement ses octets originaux.
/// - Avec BOM UTF-8 : décode en UTF-8, réécrit avec BOM.
/// - Sans BOM : utilise Latin1 comme encodage transparent (byte N → char N → byte N),
///   ce qui préserve les accents qu'ils soient en UTF-8 sans BOM ou en ANSI/Windows-1252.
/// </summary>
public static class FileEncodingHelper
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public static async Task<(string content, Encoding encoding)> ReadFilePreservingEncodingAsync(string filePath)
    {
        var rawBytes = await File.ReadAllBytesAsync(filePath);
        var hasBom = rawBytes.Length >= 3
            && rawBytes[0] == Utf8Bom[0]
            && rawBytes[1] == Utf8Bom[1]
            && rawBytes[2] == Utf8Bom[2];
        var encoding = hasBom
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            : (Encoding)Encoding.Latin1;
        var content = encoding.GetString(rawBytes, hasBom ? 3 : 0, rawBytes.Length - (hasBom ? 3 : 0));
        return (content, encoding);
    }
}
