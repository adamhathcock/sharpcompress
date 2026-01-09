using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    private const int BufferSize = 81920;

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
            sourceStream.CopyTo(streamToWriteTo, BufferSize);
        }

        /// <summary>
        /// Extract entry to the specified stream asynchronously.
        /// </summary>
        /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public async Task WriteToAsync(
            Stream streamToWriteTo,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }

            using var entryStream = await archiveEntry.OpenEntryStreamAsync(cancellationToken);
            var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
            await sourceStream
                .CopyToAsync(streamToWriteTo, BufferSize, cancellationToken)
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
            ExtractionMethods.WriteEntryToDirectory(
                entry,
                destinationDirectory,
                options,
                entry.WriteToFile
            );

        /// <summary>
        /// Extract to specific directory asynchronously, retaining filename
        /// </summary>
        public Task WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ExtractionMethods.WriteEntryToDirectoryAsync(
                entry,
                destinationDirectory,
                options,
                entry.WriteToFileAsync,
                cancellationToken
            );

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public void WriteToFile(string destinationFileName, ExtractionOptions? options = null) =>
            ExtractionMethods.WriteEntryToFile(
                entry,
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
        public Task WriteToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ExtractionMethods.WriteEntryToFileAsync(
                entry,
                destinationFileName,
                options,
                async (x, fm, ct) =>
                {
                    using var fs = File.Open(destinationFileName, fm);
                    await entry.WriteToAsync(fs, null, ct).ConfigureAwait(false);
                },
                cancellationToken
            );
    }
}
