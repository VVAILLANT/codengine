using CoreHelper = Codengine.Core.IO.FileEncodingHelper;

namespace Codengine.Cli;

internal static class FileEncodingHelper
{
    public static Task<(string content, System.Text.Encoding encoding)> ReadFileAsync(string filePath)
        => CoreHelper.ReadFilePreservingEncodingAsync(filePath);
}
