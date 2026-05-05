using System;
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
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (
                await archive.IsSolidAsync().ConfigureAwait(false)
                || archive.Type == ArchiveType.SevenZip
            )
            {
                await using var reader = await archive
                    .ExtractAllEntriesAsync()
                    .ConfigureAwait(false);
                await reader
                    .WriteAllToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await archive
                    .WriteToDirectoryAsyncInternal(
                        destinationDirectory,
                        options,
                        progress,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        private async ValueTask WriteToDirectoryAsyncInternal(
            string destinationDirectory,
            ExtractionOptions? options,
            IProgress<ProgressReport>? progress,
            CancellationToken cancellationToken
        )
        {
            options ??= new ExtractionOptions();
            var fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
                destinationDirectory
            );

            var totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
            var bytesRead = 0L;

            await foreach (var entry in archive.EntriesAsync.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    await entry
                        .WriteEntryToDirectoryAsyncCore(
                            fullDestinationDirectoryPath,
                            options,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                await entry
                    .WriteEntryToDirectoryAsyncCore(
                        fullDestinationDirectoryPath,
                        options,
                        async (path, ct) =>
                            await entry.WriteToFileAsync(path, options, ct).ConfigureAwait(false),
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
