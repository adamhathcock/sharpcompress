using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive
{
    protected override async ValueTask SaveToAsync(
        Stream stream,
        TarWriterOptions options,
        IAsyncEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries,
        CancellationToken cancellationToken = default
    )
    {
        await using var writer = new TarWriter(stream, options);
        await foreach (
            var entry in oldEntries.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            if (entry.IsDirectory)
            {
                await writer
                    .WriteDirectoryAsync(
                        entry.Key.NotNull("Entry Key is null"),
                        entry.LastModifiedTime,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
#pragma warning disable VSTHRD103
                using var entryStream = entry.OpenEntryStream();
#pragma warning restore VSTHRD103
                await writer
                    .WriteAsync(
                        entry.Key.NotNull("Entry Key is null"),
                        entryStream,
                        entry.LastModifiedTime,
                        entry.Size,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
        foreach (var entry in newEntries)
        {
            if (entry.IsDirectory)
            {
                await writer
                    .WriteDirectoryAsync(
                        entry.Key.NotNull("Entry Key is null"),
                        entry.LastModifiedTime,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
#pragma warning disable VSTHRD103
                using var entryStream = entry.OpenEntryStream();
#pragma warning restore VSTHRD103
                await writer
                    .WriteAsync(
                        entry.Key.NotNull("Entry Key is null"),
                        entryStream,
                        entry.LastModifiedTime,
                        entry.Size,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return new(new TarReader(stream, ReaderOptions, CompressionType.None));
    }

    protected override async IAsyncEnumerable<TarArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<TarVolume> volumes
    )
    {
        var stream = (await volumes.SingleAsync().ConfigureAwait(false)).Stream;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await foreach (
            var header in TarHeaderFactory.ReadHeaderAsync(
                StreamingMode.Seekable,
                stream,
                ReaderOptions.ArchiveEncoding
            )
        )
        {
            if (header != null)
            {
                yield return new TarArchiveEntry(
                    this,
                    new TarFilePart(header, stream),
                    CompressionType.None,
                    ReaderOptions
                );
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
    }
}
