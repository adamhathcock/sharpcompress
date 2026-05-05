using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    /// <param name="archiveEntry">The archive entry to extract.</param>
    extension(IArchiveEntry archiveEntry)
    {
        /// <summary>
        /// Extract entry to the specified stream.
        /// </summary>
        /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public void WriteTo(Stream streamToWriteTo, IProgress<ProgressReport>? progress = null)
        {
            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }

            using var entryStream = archiveEntry.OpenEntryStream();
            var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
            sourceStream.CopyTo(streamToWriteTo, Constants.BufferSize);
        }

        /// <summary>
        /// Extract entry to the specified stream asynchronously.
        /// </summary>
        /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public async ValueTask WriteToAsync(
            Stream streamToWriteTo,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }

#if LEGACY_DOTNET
            using var entryStream = await archiveEntry
                .OpenEntryStreamAsync(cancellationToken)
                .ConfigureAwait(false);
#else
            await using var entryStream = await archiveEntry
                .OpenEntryStreamAsync(cancellationToken)
                .ConfigureAwait(false);
#endif
            var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
            await sourceStream
                .CopyToAsync(streamToWriteTo, Constants.BufferSize, cancellationToken)
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
        public void WriteToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null
        ) =>
            entry.WriteEntryToDirectory(
                destinationDirectory,
                options,
                (path) => entry.WriteToFile(path, options)
            );

        /// <summary>
        /// Extract to specific directory asynchronously, retaining filename
        /// </summary>
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
        public void WriteToFile(string destinationFileName, ExtractionOptions? options = null) =>
            entry.WriteEntryToFile(
                destinationFileName,
                options,
                (x, fm) =>
                {
                    using var fs = File.Open(destinationFileName, fm);
                    entry.WriteTo(fs);
                }
            );

        /// <summary>
        /// Extract to specific file asynchronously
        /// </summary>
        public async ValueTask WriteToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await entry
                .WriteEntryToFileAsync(
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(destinationFileName, fm);
                        await entry.WriteToAsync(fs, null, ct).ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
    }
}
