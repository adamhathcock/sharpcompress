using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pure.DI;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace SharpCompress.Services;

// Destination streams are borrowed, not owned by the reusable extraction service.
[GenerateInterface(asInternal: true)]
internal sealed partial class ArchiveEntryExtractor : IArchiveEntryExtractor
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
        var sourceStream = ExtractionStreamHelper.WrapWithProgress(
            checkedStream,
            archiveEntry,
            progress
        );
        await sourceStream
            .CopyToAsync(streamToWriteTo, bufferSize ?? Constants.BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }
}
