using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Extraction;

internal sealed class ArchiveExtractionCoordinator
{
    private readonly IAsyncArchive archive;
    private readonly string destinationDirectory;
    private readonly ExtractionOptions options;
    private readonly IProgress<ProgressReport>? progress;
    private readonly CancellationToken cancellationToken;
    private readonly string fullDestinationDirectoryPath;
    private List<IArchiveEntry>? entries;
    private long bytesRead;
    private long? totalBytes;

    internal ArchiveExtractionCoordinator(
        IAsyncArchive archive,
        string destinationDirectory,
        ExtractionOptions? options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken
    )
    {
        this.archive = archive;
        this.destinationDirectory = destinationDirectory;
        this.options = options ?? new ExtractionOptions();
        this.progress = progress;
        this.cancellationToken = cancellationToken;
        ValidateOptions(this.options);
        fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
            destinationDirectory
        );
    }

    internal async ValueTask WriteToDirectoryAsync()
    {
        if (options.Parallelism == ExtractionParallelism.SingleThreaded)
        {
            await WriteSequentiallyAsync().ConfigureAwait(false);
            return;
        }

        var concurrencyInfo = await GetExtractionConcurrencyInfoAsync().ConfigureAwait(false);
        if (CanExtractZipEntriesInParallel(concurrencyInfo))
        {
            await WriteZipEntriesInParallelAsync(concurrencyInfo).ConfigureAwait(false);
            return;
        }

        if (CanExtractGroupsInParallel(concurrencyInfo))
        {
            await WriteArchiveGroupsInParallelAsync(concurrencyInfo).ConfigureAwait(false);
            return;
        }

        if (options.Parallelism == ExtractionParallelism.RequireParallel)
        {
            throw new NotSupportedException(
                "This archive cannot be extracted in parallel because it requires sequential reading or is not backed by an independently readable file."
            );
        }

        await WriteSequentiallyAsync().ConfigureAwait(false);
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

    private async ValueTask<ArchiveExtractionConcurrencyInfo> GetExtractionConcurrencyInfoAsync()
    {
        if (archive is IArchiveExtractionConcurrencyProvider provider)
        {
            return await provider
                .GetExtractionConcurrencyInfoAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new ArchiveExtractionConcurrencyInfo(archive.Type);
    }

    private static bool CanExtractZipEntriesInParallel(
        ArchiveExtractionConcurrencyInfo concurrencyInfo
    ) =>
        concurrencyInfo.Type == ArchiveType.Zip
        && concurrencyInfo.Mode == ArchiveConcurrencyMode.IndependentEntries
        && concurrencyInfo.SupportsIndependentEntryStreams
        && concurrencyInfo.SourceFile is not null;

    private static bool CanExtractGroupsInParallel(
        ArchiveExtractionConcurrencyInfo concurrencyInfo
    ) =>
        concurrencyInfo.SupportsIndependentSolidStreams
        && concurrencyInfo.SourceFile is not null
        && concurrencyInfo.Groups.Count > 1
        && (
            concurrencyInfo.Type == ArchiveType.SevenZip || concurrencyInfo.Type == ArchiveType.Rar
        );

    private async ValueTask WriteZipEntriesInParallelAsync(
        ArchiveExtractionConcurrencyInfo concurrencyInfo
    )
    {
        var archiveEntries = await LoadEntriesAsync().ConfigureAwait(false);
        ValidateOutputPaths(archiveEntries);
        await WriteDirectoryEntriesAsync(archiveEntries).ConfigureAwait(false);

        var sourceFile = concurrencyInfo.SourceFile.NotNull();
        using var throttler = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var fileEntries = archiveEntries.Where(entry => !entry.IsDirectory).ToList();

        var tasks = fileEntries.Select(async entry =>
        {
            await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExtractZipEntryFromNewArchiveAsync(
                        sourceFile,
                        concurrencyInfo.ReaderOptions,
                        entry.Key
                    )
                    .ConfigureAwait(false);
                await ReportProgressAsync(entry).ConfigureAwait(false);
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

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async ValueTask WriteArchiveGroupsInParallelAsync(
        ArchiveExtractionConcurrencyInfo concurrencyInfo
    )
    {
        var archiveEntries = await LoadEntriesAsync().ConfigureAwait(false);
        ValidateOutputPaths(archiveEntries);
        await WriteDirectoryEntriesAsync(archiveEntries).ConfigureAwait(false);

        using var throttler = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var tasks = concurrencyInfo.Groups.Select(async group =>
        {
            await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExtractGroupFromNewArchiveAsync(concurrencyInfo, group).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArchiveExtractionException(
                    "Failed to extract an archive entry group.",
                    group.EntryKeys.FirstOrDefault(),
                    ex
                );
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async ValueTask ExtractGroupFromNewArchiveAsync(
        ArchiveExtractionConcurrencyInfo concurrencyInfo,
        ArchiveExtractionGroup group
    )
    {
        var sourceFile = concurrencyInfo.SourceFile.NotNull();
        await using var workerArchive = await OpenWorkerArchiveAsync(concurrencyInfo, sourceFile)
            .ConfigureAwait(false);
        var entryKeys = new HashSet<string>(group.EntryKeys, StringComparer.Ordinal);

        await foreach (
            var entry in workerArchive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            if (entry.IsDirectory || entry.Key is null || !entryKeys.Contains(entry.Key))
            {
                continue;
            }

            await entry
                .WriteToDirectoryAsync(destinationDirectory, options, cancellationToken)
                .ConfigureAwait(false);
            await ReportProgressAsync(entry).ConfigureAwait(false);
        }
    }

    private async ValueTask<IAsyncArchive> OpenWorkerArchiveAsync(
        ArchiveExtractionConcurrencyInfo concurrencyInfo,
        FileInfo sourceFile
    )
    {
        return concurrencyInfo.Type switch
        {
            ArchiveType.SevenZip => await SevenZipArchive
                .OpenAsyncArchive(sourceFile, concurrencyInfo.ReaderOptions, cancellationToken)
                .ConfigureAwait(false),
            ArchiveType.Rar => await RarArchive
                .OpenAsyncArchive(sourceFile, concurrencyInfo.ReaderOptions, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new NotSupportedException(
                $"Parallel group extraction is not supported for {concurrencyInfo.Type}."
            ),
        };
    }

    private async ValueTask ExtractZipEntryFromNewArchiveAsync(
        FileInfo sourceFile,
        ReaderOptions? readerOptions,
        string? entryKey
    )
    {
        await using var workerArchive = await ZipArchive
            .OpenAsyncArchive(sourceFile, readerOptions, cancellationToken)
            .ConfigureAwait(false);
        IArchiveEntry? entry = null;
        await foreach (
            var candidate in workerArchive
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

    private async ValueTask WriteSequentiallyAsync()
    {
        if (
            await archive.IsSolidAsync().ConfigureAwait(false)
            || archive.Type == ArchiveType.SevenZip
        )
        {
            await using var reader = await archive.ExtractAllEntriesAsync().ConfigureAwait(false);
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await reader
                    .WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);

                if (!reader.Entry.IsDirectory)
                {
                    await ReportProgressAsync(reader.Entry).ConfigureAwait(false);
                }
            }
        }
        else
        {
            await WriteIndependentEntriesSequentiallyAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask WriteIndependentEntriesSequentiallyAsync()
    {
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

            await ReportProgressAsync(entry).ConfigureAwait(false);
        }
    }

    private async ValueTask<List<IArchiveEntry>> LoadEntriesAsync()
    {
        if (entries is not null)
        {
            return entries;
        }

        entries = new List<IArchiveEntry>();
        await foreach (
            var entry in archive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            entries.Add(entry);
        }

        return entries;
    }

    private async ValueTask<long> GetTotalBytesAsync()
    {
        if (!totalBytes.HasValue)
        {
            totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
        }

        return totalBytes.Value;
    }

    private async ValueTask ReportProgressAsync(IEntry entry)
    {
        if (progress is null)
        {
            return;
        }

        var extracted = Interlocked.Add(ref bytesRead, entry.Size);
        progress.Report(
            new ProgressReport(
                entry.Key ?? string.Empty,
                extracted,
                await GetTotalBytesAsync().ConfigureAwait(false)
            )
        );
    }

    private async ValueTask WriteDirectoryEntriesAsync(IEnumerable<IArchiveEntry> archiveEntries)
    {
        foreach (var directory in archiveEntries.Where(entry => entry.IsDirectory))
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
    }

    private void ValidateOutputPaths(IReadOnlyList<IArchiveEntry> archiveEntries)
    {
        var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archiveEntries)
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
}
