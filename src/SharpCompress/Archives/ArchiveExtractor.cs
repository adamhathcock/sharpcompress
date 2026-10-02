using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>A stateless extraction workflow shared by archive and reader APIs.</summary>
public sealed partial class ArchiveExtractor : IArchiveExtractor
{
    public void ExtractToDirectory(
        IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null
    )
    {
        ThrowHelper.ThrowIfNull(archive);
        options ??= new ExtractionOptions();
        var destination = DirectoryManagement.GetFullDestinationDirectoryPath(destinationDirectory);
        var totalBytes = archive.TotalUncompressedSize;
        var bytesRead = 0L;
        // Solid archive decoding must remain sequential to retain dictionary state.
        if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                ExtractEntryToDirectory(reader, destination, options);
                ReportCompletedEntry(reader.Entry, progress, ref bytesRead, totalBytes);
            }
        }
        else
        {
            foreach (var entry in archive.Entries)
            {
                ExtractToDirectory(entry, destination, options);
                ReportCompletedEntry(entry, progress, ref bytesRead, totalBytes);
            }
        }
    }

    public async ValueTask ExtractToDirectoryAsync(
        IAsyncArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(archive);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ExtractionOptions();
        var destination = DirectoryManagement.GetFullDestinationDirectoryPath(destinationDirectory);
        var totalBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);
        var bytesRead = 0L;
        if (
            await archive.IsSolidAsync().ConfigureAwait(false)
            || archive.Type == ArchiveType.SevenZip
        )
        {
            await using var reader = await archive.ExtractAllEntriesAsync().ConfigureAwait(false);
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExtractEntryToDirectoryAsync(reader, destination, options, cancellationToken)
                    .ConfigureAwait(false);
                ReportCompletedEntry(reader.Entry, progress, ref bytesRead, totalBytes);
            }
        }
        else
        {
            await foreach (
                var entry in archive
                    .EntriesAsync.WithCancellation(cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                await ExtractToDirectoryAsync(entry, destination, options, cancellationToken)
                    .ConfigureAwait(false);
                ReportCompletedEntry(entry, progress, ref bytesRead, totalBytes);
            }
        }
    }

    public void ExtractToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        var destination = DirectoryManagement.GetFullDestinationDirectoryPath(destinationDirectory);
        while (reader.MoveToNextEntry())
        {
            ExtractEntryToDirectory(reader, destination, options);
        }
    }

    public async ValueTask ExtractToDirectoryAsync(
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        var destination = DirectoryManagement.GetFullDestinationDirectoryPath(destinationDirectory);
        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            await ExtractEntryToDirectoryAsync(reader, destination, options, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public void ExtractEntryToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        reader.Entry.WriteEntryToDirectory(
            destinationDirectory,
            options,
            path => ExtractToFile(reader, path, options)
        );
    }

    public ValueTask ExtractEntryToDirectoryAsync(
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        return reader.Entry.WriteEntryToDirectoryAsync(
            destinationDirectory,
            options,
            (path, token) => ExtractToFileAsync(reader, path, options, token),
            cancellationToken
        );
    }

    /// <summary>Extract entry to the specified directory.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public ValueTask ExtractToDirectoryAsync(
        IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(entry);
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif
        return entry.WriteEntryToDirectoryAsync(
            destinationDirectory,
            options,
            (path, token) => ExtractToFileAsync(entry, path, options, token),
            cancellationToken
        );
    }

    /// <summary>Extract entry to the specified file.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public ValueTask ExtractToFileAsync(
        IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(entry);
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif
        options ??= new ExtractionOptions();
        return entry.WriteEntryToFileAsync(
            destinationFileName,
            options,
            async (path, mode, token) =>
            {
                using var destination = File.Open(path, mode);
                await ExtractToStreamAsync(entry, destination, options, cancellationToken: token)
                    .ConfigureAwait(false);
            },
            cancellationToken
        );
    }

    public void ExtractToFile(
        IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        options ??= new ExtractionOptions();
        reader.Entry.WriteEntryToFile(
            destinationFileName,
            options,
            (path, mode) =>
            {
                using var destination = File.Open(path, mode);
                ExtractToStream(reader, destination, options);
            }
        );
    }

    public ValueTask ExtractToFileAsync(
        IAsyncReader reader,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ExtractionOptions();
        return reader.Entry.WriteEntryToFileAsync(
            destinationFileName,
            options,
            async (path, mode, token) =>
            {
                using var destination = File.Open(path, mode);
                await ExtractToStreamAsync(reader, destination, options, token)
                    .ConfigureAwait(false);
            },
            cancellationToken
        );
    }

    /// <summary>Extract entry to the specified stream, leaving the destination open.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
    public async ValueTask ExtractToStreamAsync(
        IArchiveEntry entry,
        Stream destination,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(entry);
        destination.RequireWritable();
        if (entry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }
#if SYNC_ONLY
        using var source = entry.OpenEntryStream();
#else
        cancellationToken.ThrowIfCancellationRequested();
        var source = await entry.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var sourceScope = source.DisposeAsyncScope().ConfigureAwait(false);
#endif
        var checkedStream = options is null
            ? source
            : IEntryExtensions.WrapWithChecksumValidation(entry, source, options);
        var progressStream = WrapWithProgress(checkedStream, entry, progress);
        await progressStream
            .CopyToAsync(
                destination,
                options?.BufferSize ?? Constants.BufferSize,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public void ExtractToStream(
        IReader reader,
        Stream destination,
        ExtractionOptions? options = null
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        destination.RequireWritable();
        options ??= new ExtractionOptions();
        using var source = reader.OpenEntryStream();
        var checkedStream = IEntryExtensions.WrapWithChecksumValidation(
            reader.Entry,
            source,
            options
        );
        var progressStream = WrapWithProgress(
            checkedStream,
            reader.Entry,
            reader.Entry.Options.Progress
        );
        progressStream.CopyTo(destination, options.BufferSize);
    }

    public async ValueTask ExtractToStreamAsync(
        IAsyncReader reader,
        Stream destination,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(reader);
        destination.RequireWritable();
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ExtractionOptions();
        await using var source = await reader
            .OpenEntryStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var checkedStream = IEntryExtensions.WrapWithChecksumValidation(
            reader.Entry,
            source,
            options
        );
        var progressStream = WrapWithProgress(
            checkedStream,
            reader.Entry,
            reader.Entry.Options.Progress
        );
        await progressStream
            .CopyToAsync(destination, options.BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Stream WrapWithProgress(
        Stream source,
        IEntry entry,
        IProgress<ProgressReport>? progress
    )
    {
        if (progress is null)
        {
            return source;
        }
        long? size;
        try
        {
            size = entry.Size >= 0 ? entry.Size : null;
        }
        catch (NotImplementedException)
        {
            size = null;
        }
        return new ProgressReportingStream(
            source,
            progress,
            entry.Key ?? string.Empty,
            size,
            leaveOpen: true
        );
    }

    private static void ReportCompletedEntry(
        IEntry entry,
        IProgress<ProgressReport>? progress,
        ref long bytesRead,
        long totalBytes
    )
    {
        if (!entry.IsDirectory && progress is not null)
        {
            bytesRead += entry.Size;
            progress.Report(new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes));
        }
    }
}
