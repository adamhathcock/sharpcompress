using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IArchiveExtensions
{
    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static async Task WriteToDirectoryAsync(
        this IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var reader = archive.ExtractAllEntries();
        await reader.WriteAllToDirectoryAsync(destinationDirectory, options);
    }

    /// <summary>
    /// Extracts the archive to the destination directory. Directories will be created as needed.
    /// </summary>
    /// <param name="archive">The archive to extract.</param>
    /// <param name="destination">The folder to extract into.</param>
    /// <param name="progressReport">Optional progress report callback.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task ExtractToDirectory(
        this IArchive archive,
        string destination,
        Action<double>? progressReport = null,
        CancellationToken cancellationToken = default
    )
    {
        // Prepare for progress reporting
        var totalBytes = archive.TotalUncompressSize;
        var bytesRead = 0L;

        // Tracking for created directories.
        var seenDirectories = new HashSet<string>();

        // Extract
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsDirectory)
            {
                var dirPath = Path.Combine(destination, entry.Key.NotNull("Entry Key is null"));
                if (
                    Path.GetDirectoryName(dirPath + "/") is { } emptyDirectory
                    && seenDirectories.Add(dirPath)
                )
                {
                    Directory.CreateDirectory(emptyDirectory);
                }
                continue;
            }

            // Create each directory if not already created
            var path = Path.Combine(destination, entry.Key.NotNull("Entry Key is null"));
            if (Path.GetDirectoryName(path) is { } directory)
            {
                if (!Directory.Exists(directory) && !seenDirectories.Contains(directory))
                {
                    Directory.CreateDirectory(directory);
                    seenDirectories.Add(directory);
                }
            }

            // Write file
            using var fs = File.OpenWrite(path);
            entry.WriteTo(fs);

            // Update progress
            bytesRead += entry.Size;
            progressReport?.Invoke(bytesRead / (double)totalBytes);
        }
    }
}
