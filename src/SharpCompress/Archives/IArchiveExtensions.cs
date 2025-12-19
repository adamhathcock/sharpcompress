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
    public static void WriteToDirectory(
        this IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        // For solid archives (Rar, 7Zip), use the optimized reader-based approach
        if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            reader.WriteAllToDirectory(destinationDirectory, options);
        }
        else
        {
            // For non-solid archives, extract entries directly
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    entry.WriteToDirectory(destinationDirectory, options);
                }
            }
        }
    }

    /// <summary>
    /// Extract to specific directory with progress reporting and cancellation support
    /// </summary>
    /// <param name="archive">The archive to extract.</param>
    /// <param name="destinationDirectory">The folder to extract into.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static void WriteToDirectory(
        this IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options,
        IProgress<ProgressReport>? progress,
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
                var dirPath = Path.Combine(
                    destinationDirectory,
                    entry.Key.NotNull("Entry Key is null")
                );
                if (
                    Path.GetDirectoryName(dirPath + "/") is { } parentDirectory
                    && seenDirectories.Add(dirPath)
                )
                {
                    Directory.CreateDirectory(parentDirectory);
                }
                continue;
            }

            // Use the entry's WriteToDirectory method which respects ExtractionOptions
            entry.WriteToDirectory(destinationDirectory, options);

            // Update progress
            bytesRead += entry.Size;
            progress?.Report(new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes));
        }
    }

    /// <summary>
    /// Extract to specific directory asynchronously with progress reporting and cancellation support
    /// </summary>
    /// <param name="archive">The archive to extract.</param>
    /// <param name="destinationDirectory">The folder to extract into.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task WriteToDirectoryAsync(
        this IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
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
                var dirPath = Path.Combine(
                    destinationDirectory,
                    entry.Key.NotNull("Entry Key is null")
                );
                if (
                    Path.GetDirectoryName(dirPath + "/") is { } parentDirectory
                    && seenDirectories.Add(dirPath)
                )
                {
                    Directory.CreateDirectory(parentDirectory);
                }
                continue;
            }

            // Use the entry's WriteToDirectoryAsync method which respects ExtractionOptions
            await entry
                .WriteToDirectoryAsync(destinationDirectory, options, cancellationToken)
                .ConfigureAwait(false);

            // Update progress
            bytesRead += entry.Size;
            progress?.Report(new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes));
        }
    }
}
