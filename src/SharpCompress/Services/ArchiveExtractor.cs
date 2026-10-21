using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Services;

// Destination streams are borrowed, not owned by the reusable extraction service.
internal sealed partial class ArchiveExtractor
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
    public async ValueTask WriteToAsync(
        IArchiveEntry archiveEntry,
        Stream streamToWriteTo,
        int? bufferSize,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

#if SYNC_ONLY
        using var entryStream = archiveEntry.OpenEntryStream();
#else
        var entryStream = await archiveEntry
            .OpenEntryStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var entryStreamScope = entryStream.DisposeAsyncScope().ConfigureAwait(false);
#endif
        var checkedStream = options is null
            ? entryStream
            : IEntryExtensions.WrapWithChecksumValidation(archiveEntry, entryStream, options);
        var sourceStream = WrapWithProgress(checkedStream, archiveEntry, progress);
        await sourceStream
            .CopyToAsync(streamToWriteTo, bufferSize ?? Constants.BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Stream WrapWithProgress(
        Stream source,
        IArchiveEntry entry,
        IProgress<ProgressReport>? progress
    )
    {
        if (progress is null)
        {
            return source;
        }

        var entryPath = entry.Key ?? string.Empty;
        var totalBytes = GetEntrySizeSafe(entry);
        return new ProgressReportingStream(
            source,
            progress,
            entryPath,
            totalBytes,
            leaveOpen: true
        );
    }

    private static long? GetEntrySizeSafe(IArchiveEntry entry)
    {
        try
        {
            var size = entry.Size;
            return size >= 0 ? size : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }
}
