using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Services;

internal sealed partial class ArchiveExtractor
{
    /// <summary>
    /// Extract to specific directory asynchronously, retaining filename
    /// </summary>
    public async ValueTask WriteEntryToDirectoryAsync(
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        await reader
            .Entry.WriteEntryToDirectoryAsync(
                destinationDirectory,
                options,
                async (path, ct) =>
                    await WriteEntryToFileAsync(reader, path, options, ct).ConfigureAwait(false),
                cancellationToken
            )
            .ConfigureAwait(false);

    /// <summary>
    /// Extract to specific file asynchronously
    /// </summary>
    public async ValueTask WriteEntryToFileAsync(
        IAsyncReader reader,
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
                    var fs = File.Open(x, fm);
                    await using var scope = fs.DisposeAsyncScope().ConfigureAwait(false);
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
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteEntryToDirectoryAsync(
                    reader,
                    destinationDirectory,
                    options,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
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
}
