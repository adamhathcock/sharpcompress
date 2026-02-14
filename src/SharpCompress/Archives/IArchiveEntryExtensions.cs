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
        /// Opens the current entry as a stream that will decompress as it is read.
        /// For formats that only support sequential extraction, use archive.ExtractAllEntries().
        /// </summary>
        public Stream OpenEntryStream() => GetExtractableEntry(archiveEntry).OpenEntryStream();

        /// <summary>
        /// Opens the current entry as a stream that will decompress as it is read asynchronously.
        /// For formats that only support sequential extraction, use archive.ExtractAllEntriesAsync().
        /// </summary>
        public ValueTask<Stream> OpenEntryStreamAsync(
            CancellationToken cancellationToken = default
        ) => GetExtractableEntry(archiveEntry).OpenEntryStreamAsync(cancellationToken);

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

            var extractable = GetExtractableEntry(archiveEntry);
            using var entryStream = extractable.OpenEntryStream();
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
            using var entryStream = await GetExtractableEntry(archiveEntry)
                .OpenEntryStreamAsync(cancellationToken)
                .ConfigureAwait(false);
#else
            await using var entryStream = await GetExtractableEntry(archiveEntry)
                .OpenEntryStreamAsync(cancellationToken)
                .ConfigureAwait(false);
#endif
            var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
            await sourceStream
                .CopyToAsync(streamToWriteTo, Constants.BufferSize, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IExtractableArchiveEntry GetExtractableEntry(IArchiveEntry entry)
    {
        if (entry is IExtractableArchiveEntry extractableEntry)
        {
            return extractableEntry;
        }

        throw new NotSupportedException(
            $"Entry stream extraction is not supported for {entry.Archive.Type} archive entries. Use archive.ExtractAllEntries() for sequential extraction."
        );
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
        public void WriteToDirectory(string destinationDirectory) =>
            ExtractionMethods.WriteEntryToDirectory(
                entry,
                destinationDirectory,
                (path) => entry.WriteToFile(path)
            );

        /// <summary>
        /// Extract to specific directory asynchronously, retaining filename
        /// </summary>
        public async ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            CancellationToken cancellationToken = default
        ) =>
            await ExtractionMethods
                .WriteEntryToDirectoryAsync(
                    entry,
                    destinationDirectory,
                    async (path, ct) =>
                        await entry.WriteToFileAsync(path, ct).ConfigureAwait(false),
                    cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public void WriteToFile(string destinationFileName) =>
            ExtractionMethods.WriteEntryToFile(
                entry,
                destinationFileName,
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
            CancellationToken cancellationToken = default
        ) =>
            await ExtractionMethods
                .WriteEntryToFileAsync(
                    entry,
                    destinationFileName,
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
