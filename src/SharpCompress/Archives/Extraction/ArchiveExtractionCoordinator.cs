using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Extraction;

internal static class ArchiveExtractionCoordinator
{
    internal static async ValueTask WriteToDirectoryAsync(
        IAsyncArchive archive,
        string destinationDirectory,
        ExtractionOptions? options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken
    )
    {
        options ??= new ExtractionOptions();
        ValidateOptions(options);

        if (options.Parallelism == ExtractionParallelism.SingleThreaded)
        {
            await WriteSequentiallyAsync(
                    archive,
                    destinationDirectory,
                    options,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        var archiveInformation = await GetArchiveInformationAsync(archive).ConfigureAwait(false);
        if (
            archiveInformation.ConcurrencyMode != ArchiveConcurrencyMode.IndependentEntries
            || !archiveInformation.SupportsIndependentEntryStreams
            || archiveInformation.Type != ArchiveType.Zip
        )
        {
            if (options.Parallelism == ExtractionParallelism.RequireParallel)
            {
                throw new NotSupportedException(
                    "This archive cannot be extracted in parallel because it requires sequential reading or is not backed by an independently readable file."
                );
            }

            await WriteSequentiallyAsync(
                    archive,
                    destinationDirectory,
                    options,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        await WriteZipEntriesInParallelAsync(
                archive,
                archiveInformation.ParallelExtractionSourceFile.NotNull(),
                destinationDirectory,
                options,
                progress,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static void ValidateOptions(ExtractionOptions options)
    {
        if (options.MaxDegreeOfParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxDegreeOfParallelism must be greater than zero."
            );
        }
    }

    private static async ValueTask<ArchiveInformation> GetArchiveInformationAsync(
        IAsyncArchive archive
    )
    {
        if (archive is IArchiveExtractionConcurrencyProvider provider)
        {
            return await provider.GetArchiveInformationAsync().ConfigureAwait(false);
        }

        return new ArchiveInformation(archive.Type, supportsRandomAccess: true);
    }

    private static async ValueTask WriteZipEntriesInParallelAsync(
        IAsyncArchive archive,
        FileInfo sourceFile,
        string destinationDirectory,
        ExtractionOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken
    )
    {
        var fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
            destinationDirectory
        );
        var entries = new List<IArchiveEntry>();
        await foreach (
            var entry in archive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            entries.Add(entry);
        }
        ValidateOutputPaths(entries, fullDestinationDirectoryPath, options);

        var totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
        var bytesRead = 0L;
        using var throttler = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var fileEntries = entries.Where(entry => !entry.IsDirectory).ToList();

        var tasks = fileEntries.Select(async entry =>
        {
            await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExtractZipEntryFromNewArchiveAsync(
                        sourceFile,
                        entry.Key,
                        destinationDirectory,
                        options,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                var extracted = Interlocked.Add(ref bytesRead, entry.Size);
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, extracted, totalBytes)
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArchiveExtractionException(
                    $"Failed to extract archive entry '{entry.Key}'.",
                    entry.Key,
                    ex
                );
            }
            finally
            {
                throttler.Release();
            }
        });

        foreach (var directory in entries.Where(entry => entry.IsDirectory))
        {
            await directory
                .WriteEntryToDirectoryAsyncCore(
                    fullDestinationDirectoryPath,
                    options,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async ValueTask ExtractZipEntryFromNewArchiveAsync(
        FileInfo sourceFile,
        string? entryKey,
        string destinationDirectory,
        ExtractionOptions options,
        CancellationToken cancellationToken
    )
    {
        await using var archive = await ZipArchive
            .OpenAsyncArchive(sourceFile, readerOptions: null, cancellationToken)
            .ConfigureAwait(false);
        IArchiveEntry? entry = null;
        await foreach (
            var candidate in archive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            if (candidate.Key == entryKey && !candidate.IsDirectory)
            {
                entry = candidate;
                break;
            }
        }

        if (entry is null)
        {
            throw new ArchiveOperationException($"Archive entry '{entryKey}' was not found.");
        }

        await entry
            .WriteToDirectoryAsync(destinationDirectory, options, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateOutputPaths(
        IReadOnlyList<IArchiveEntry> entries,
        string fullDestinationDirectoryPath,
        ExtractionOptions options
    )
    {
        var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var outputPath = Path.GetFullPath(
                entry.GetEntryDestinationFileName(fullDestinationDirectoryPath, options)
            );

            if (entry.IsDirectory)
            {
                continue;
            }

            DirectoryManagement.EnsurePathInDestinationDirectory(
                outputPath,
                fullDestinationDirectoryPath,
                DirectoryManagement.WriteFileOutsideDestinationMessage
            );

            if (!outputPaths.Add(outputPath))
            {
                throw new ExtractionException(
                    "The archive contains multiple entries that resolve to the same output path."
                );
            }
        }
    }

    private static async ValueTask WriteSequentiallyAsync(
        IAsyncArchive archive,
        string destinationDirectory,
        ExtractionOptions? options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken
    )
    {
        if (
            await archive.IsSolidAsync().ConfigureAwait(false)
            || archive.Type == ArchiveType.SevenZip
        )
        {
            var totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
            var bytesRead = 0L;
            await using var reader = await archive.ExtractAllEntriesAsync().ConfigureAwait(false);
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await reader
                    .WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);

                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                bytesRead += reader.Entry.Size;
                progress?.Report(
                    new ProgressReport(reader.Entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
        else
        {
            await WriteIndependentEntriesSequentiallyAsync(
                    archive,
                    destinationDirectory,
                    options,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteIndependentEntriesSequentiallyAsync(
        IAsyncArchive archive,
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

        await foreach (
            var entry in archive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
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
            progress?.Report(new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes));
        }
    }
}
