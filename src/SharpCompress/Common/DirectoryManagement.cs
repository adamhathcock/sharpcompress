using System.IO;

namespace SharpCompress.Common;

internal static class DirectoryManagement
{
    internal const string CreateDirectoryOutsideDestinationMessage =
        "Entry is trying to create a directory outside of the destination directory.";
    internal const string WriteFileOutsideDestinationMessage =
        "Entry is trying to write a file outside of the destination directory.";

    internal static string GetFullDestinationDirectoryPath(string destinationDirectory)
    {
        var fullDestinationDirectoryPath = Path.GetFullPath(destinationDirectory);

        // Keep the trailing separator so prefix checks cannot match sibling directories.
        if (
            !IsDirectorySeparator(
                fullDestinationDirectoryPath[fullDestinationDirectoryPath.Length - 1]
            )
        )
        {
            fullDestinationDirectoryPath += Path.DirectorySeparatorChar;
        }

        if (!Directory.Exists(fullDestinationDirectoryPath))
        {
            throw new ExtractionException(
                $"Directory does not exist to extract to: {fullDestinationDirectoryPath}"
            );
        }

        return fullDestinationDirectoryPath;
    }

    internal static void EnsurePathInDestinationDirectory(
        string destinationPath,
        string fullDestinationDirectoryPath,
        string exceptionMessage
    )
    {
        if (destinationPath.StartsWith(fullDestinationDirectoryPath, Utility.PathComparison))
        {
            return;
        }

        if (
            string.Equals(
                destinationPath,
                TrimTrailingDirectorySeparators(fullDestinationDirectoryPath),
                Utility.PathComparison
            )
        )
        {
            return;
        }

        throw new ExtractionException(exceptionMessage);
    }

    private static bool IsDirectorySeparator(char value) =>
        value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;

    private static string TrimTrailingDirectorySeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        var rootLength = root?.Length ?? 0;
        var end = path.Length;

        while (end > rootLength && IsDirectorySeparator(path[end - 1]))
        {
            end--;
        }

        return end == path.Length ? path : path.Substring(0, end);
    }
}
