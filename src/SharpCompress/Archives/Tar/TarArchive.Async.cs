using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
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
        using var writer = new TarWriter(stream, options);
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
                using var entryStream = entry.OpenEntryStream();
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
                using var entryStream = entry.OpenEntryStream();
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
        return new((IAsyncReader)new TarReader(stream, ReaderOptions, _compressionType));
    }

    protected override async IAsyncEnumerable<TarArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<TarVolume> volumes
    )
    {
        var sourceStream = (await volumes.SingleAsync().ConfigureAwait(false)).Stream;
        var stream = await GetStreamAsync(sourceStream).ConfigureAwait(false);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var streamingMode =
            _compressionType == CompressionType.None
                ? StreamingMode.Seekable
                : StreamingMode.Streaming;

        // Always use async header reading in LoadEntriesAsync for consistency
        {
            // Use async header reading for async-only streams
            TarHeader? previousHeader = null;
            await foreach (
                var header in TarHeaderFactory.ReadHeaderAsync(
                    streamingMode,
                    stream,
                    ReaderOptions.ArchiveEncoding
                )
            )
            {
                if (header != null)
                {
                    if (header.EntryType == EntryType.LongName)
                    {
                        previousHeader = header;
                    }
                    else
                    {
                        if (previousHeader != null)
                        {
                            var entry = new TarArchiveEntry(
                                this,
                                new TarFilePart(
                                    previousHeader,
                                    _compressionType == CompressionType.None ? stream : null
                                ),
                                CompressionType.None,
                                ReaderOptions
                            );

                            var oldStreamPos = stream.Position;

                            using (var entryStream = entry.OpenEntryStream())
                            {
                                using var memoryStream = new MemoryStream();
                                await entryStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                                memoryStream.Position = 0;
                                var bytes = memoryStream.ToArray();

                                header.Name = ReaderOptions
                                    .ArchiveEncoding.Decode(bytes)
                                    .TrimNulls();
                            }

                            stream.Position = oldStreamPos;

                            previousHeader = null;
                        }
                        yield return new TarArchiveEntry(
                            this,
                            new TarFilePart(
                                header,
                                _compressionType == CompressionType.None ? stream : null
                            ),
                            CompressionType.None,
                            ReaderOptions
                        );
                    }
                }
                else
                {
                    throw new IncompleteArchiveException("Failed to read TAR header");
                }
            }
        }
    }
}
