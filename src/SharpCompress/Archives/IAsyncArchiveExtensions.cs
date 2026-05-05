using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IAsyncArchiveExtensions
{
    /// <summary>
    /// Gets the appropriate StringComparison for path checks based on the file system.
    /// Windows uses case-insensitive file systems, while Unix-like systems use case-sensitive file systems.
    /// </summary>
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    extension(IAsyncArchive archive)
    {
        /// <summary>
        /// Extract to specific directory asynchronously with progress reporting and cancellation support
        /// </summary>
        /// <param name="archive">The archive to extract.</param>
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
            var fullDestinationDirectoryPath = Path.GetFullPath(destinationDirectory);
            options ??= new ExtractionOptions();

            //check for trailing slash.
            if (
                fullDestinationDirectoryPath[fullDestinationDirectoryPath.Length - 1]
                != Path.DirectorySeparatorChar
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

            var totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
            var bytesRead = 0L;
            var seenDirectories = new HashSet<string>();

            await foreach (var entry in archive.EntriesAsync.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    var folder = Path.GetDirectoryName(entry.Key.NotNull("Entry Key is null"))
                        .NotNull("Directory is null");
                    var destdir = Path.GetFullPath(
                        Path.Combine(fullDestinationDirectoryPath, folder)
                    );

                    if (!Directory.Exists(destdir) && seenDirectories.Add(destdir))
                    {
                        if (!destdir.StartsWith(fullDestinationDirectoryPath, PathComparison))
                        {
                            throw new ExtractionException(
                                "Entry is trying to create a directory outside of the destination directory."
                            );
                        }

                        Directory.CreateDirectory(destdir);
                    }
                    continue;
                }

                await entry
                    .WriteToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
