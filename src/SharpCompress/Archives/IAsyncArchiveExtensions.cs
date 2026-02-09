using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IAsyncArchiveExtensions
{
    extension(IAsyncArchive archive)
    {
        /// <summary>
        /// Extract to specific directory asynchronously with progress reporting and cancellation support
        /// </summary>
        /// <param name="archive">The archive to extract.</param>
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (await archive.IsSolidAsync() || archive.Type == ArchiveType.SevenZip)
            {
                await using var reader = await archive.ExtractAllEntriesAsync();
                await reader
                    .WriteAllToDirectoryAsync(destinationDirectory, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await archive.WriteToDirectoryAsyncInternal(
                    destinationDirectory,
                    progress,
                    cancellationToken
                );
            }
        }

        private async ValueTask WriteToDirectoryAsyncInternal(
            string destinationDirectory,
            IProgress<ProgressReport>? progress,
            CancellationToken cancellationToken
        )
        {
            var totalBytes = await archive.TotalUncompressedSizeAsync();
            var bytesRead = 0L;
            var seenDirectories = new HashSet<string>();

            // When extracting an entire archive, default to extracting with full paths
            options ??= new ExtractionOptions { ExtractFullPath = true, Overwrite = true };

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

                await entry
                    .WriteToDirectoryAsync(destinationDirectory, cancellationToken)
                    .ConfigureAwait(false);

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
