using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static partial class IArchiveEntryExtensions
{
    /// <param name="archiveEntry">The archive entry to extract.</param>
    extension(IArchiveEntry archiveEntry)
    {
        /// <summary>
        /// Extract entry to the specified stream.
        /// </summary>
        /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
        public async ValueTask WriteToAsync(
            Stream streamToWriteTo,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            await archiveEntry
                .WriteToAsync(
                    streamToWriteTo,
                    Constants.BufferSize,
                    progress: progress,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract entry to the specified stream.
        /// </summary>
        /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
        /// <param name="options">Options for configuring extraction behavior.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
        public async ValueTask WriteToAsync(
            Stream streamToWriteTo,
            ExtractionOptions options,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            await archiveEntry
                .WriteToAsync(
                    streamToWriteTo,
                    options.BufferSize,
                    options,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);

        [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
        private async ValueTask WriteToAsync(
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
            await using var entryStreamScope = entryStream
                .DisposeAsyncScope()
                .ConfigureAwait(false);
#endif
            var checkedStream = options is null
                ? entryStream
                : IEntryExtensions.WrapWithChecksumValidation(archiveEntry, entryStream, options);
            var sourceStream = WrapWithProgress(checkedStream, archiveEntry, progress);
            await sourceStream
                .CopyToAsync(streamToWriteTo, bufferSize ?? Constants.BufferSize, cancellationToken)
                .ConfigureAwait(false);
        }
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

    extension(IArchiveEntry entry)
    {
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await entry
                .WriteEntryToDirectoryAsync(
                    destinationDirectory,
                    options,
                    async (path, ct) =>
                        await entry.WriteToFileAsync(path, options, ct).ConfigureAwait(false),
                    cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract to specific file
        /// </summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async ValueTask WriteToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            options ??= new ExtractionOptions();
            await entry
                .WriteEntryToFileAsync(
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(x, fm);
                        await entry
                            .WriteToAsync(fs, options.BufferSize, options, null, ct)
                            .ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }
}
