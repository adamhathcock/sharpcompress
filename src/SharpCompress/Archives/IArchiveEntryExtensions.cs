using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Services;

namespace SharpCompress.Archives;

public static partial class IArchiveEntryExtensions
{
    // Give the sync generator a known receiver type before Pure.DI generates the root property.
    private static IArchiveExtractor Extractor => LibraryDefaults.Composition.Extractor;

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
        ) =>
            await Extractor
                .WriteToAsync(
                    archiveEntry,
                    streamToWriteTo,
                    bufferSize,
                    options,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
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
            await Extractor
                .WriteToDirectoryAsync(entry, destinationDirectory, options, cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Extract to specific file
        /// </summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async ValueTask WriteToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await Extractor
                .WriteToFileAsync(entry, destinationFileName, options, cancellationToken)
                .ConfigureAwait(false);
    }
}
