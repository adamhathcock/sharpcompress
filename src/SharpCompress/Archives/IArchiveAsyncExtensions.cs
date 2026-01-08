using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IArchiveAsyncExtensions
{
    /// <param name="archive">The archive to extract.</param>
    extension(IArchiveAsync archive)
    {
        /// <summary>
        /// Extract to specific directory asynchronously with progress reporting and cancellation support
        /// </summary>
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            // For solid archives (Rar, 7Zip), use the optimized reader-based approach
            if (await archive.IsSolidAsync() || archive.Type == ArchiveType.SevenZip)
            {
                using var reader = await archive.ExtractAllEntriesAsync();
                await reader.WriteAllToDirectoryAsync(
                    destinationDirectory,
                    options,
                    cancellationToken
                );
            }
            else
            {
                // For non-solid archives, extract entries directly
                await archive.WriteToDirectoryAsyncInternal(
                    destinationDirectory,
                    options,
                    progress,
                    cancellationToken
                );
            }
        }

        private async Task WriteToDirectoryAsyncInternal(
            string destinationDirectory,
            ExtractionOptions? options,
            IProgress<ProgressReport>? progress,
            CancellationToken cancellationToken
        )
        {
            // Prepare for progress reporting
            var totalBytes = await archive.TotalUncompressSizeAsync();
            var bytesRead = 0L;

            // Tracking for created directories.
            var seenDirectories = new HashSet<string>();

            // Extract
            await foreach (var entry in archive.EntriesAsync.WithCancellation(cancellationToken))
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
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
