using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static class IAsyncReaderExtensions
{
    extension(IAsyncReader reader)
    {
        /// <summary>
        /// Extract to specific directory asynchronously, retaining filename
        /// </summary>
        public async ValueTask WriteEntryToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await reader
                .Entry.WriteEntryToDirectoryAsync(
                    destinationDirectory,
                    options,
                    async (path, ct) =>
                        await reader.WriteEntryToFileAsync(path, options, ct).ConfigureAwait(false),
                    cancellationToken
                )
                .ConfigureAwait(false);

        /// <summary>
        /// Extract to specific file asynchronously
        /// </summary>
        public async ValueTask WriteEntryToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            options ??= new ExtractionOptions();
            await reader
                .Entry.WriteEntryToFileAsync(
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(x, fm);
                        await CopyEntryToAsync(reader, fs, options, ct).ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Extract all remaining unread entries to specific directory asynchronously, retaining filename
        /// </summary>
        public async ValueTask WriteAllToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                await reader
                    .WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask WriteEntryToAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            options ??= new ExtractionOptions();
            await reader
                .Entry.WriteEntryToFileAsync(
                    destinationFileName,
                    options,
                    async (x, fm, ct) =>
                    {
                        using var fs = File.Open(x, fm);
                        await CopyEntryToAsync(reader, fs, options, ct).ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        public async ValueTask WriteEntryToAsync(
            FileInfo destinationFileInfo,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            await reader
                .WriteEntryToAsync(destinationFileInfo.FullName, options, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async ValueTask CopyEntryToAsync(
        IAsyncReader reader,
        Stream writableStream,
        ExtractionOptions options,
        CancellationToken cancellationToken
    )
    {
        await using var entryStream = await reader
            .OpenEntryStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var checkedStream = IEntryExtensions.WrapWithChecksumValidation(
            reader.Entry,
            entryStream,
            options
        );
        var sourceStream = WrapWithProgress(checkedStream, reader.Entry);
        await sourceStream
            .CopyToAsync(writableStream, options.BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Stream WrapWithProgress(Stream source, IEntry entry)
    {
        var progress = entry.Options.Progress;
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

    private static long? GetEntrySizeSafe(IEntry entry)
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
