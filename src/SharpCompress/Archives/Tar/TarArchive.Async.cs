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
                    // For compressed (streaming) archives, buffer each entry's packed stream into
                    // memory immediately. This ensures the underlying compressed stream advances
                    // past each entry's data, and allows entry streams to be opened after the
                    // full entry list has been enumerated.
                    if (_compressionType != CompressionType.None && header.PackedStream != null)
                    {
                        var buffered = new MemoryStream();
#if NET8_0_OR_GREATER
                        await using (header.PackedStream)
#else
                        using (header.PackedStream)
#endif
                        {
                            await header.PackedStream.CopyToAsync(buffered).ConfigureAwait(false);
                            // Disposing TarReadOnlySubStream skips any remaining bytes and padding
                        }
                        buffered.Position = 0;
                        header.PackedStream = buffered;
                    }

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

                            if (stream.CanSeek)
                            {
                                var oldStreamPos = stream.Position;

                                using (var entryStream = entry.OpenEntryStream())
                                {
                                    using var memoryStream = new MemoryStream();
                                    await entryStream
                                        .CopyToAsync(memoryStream)
                                        .ConfigureAwait(false);
                                    var bytes = memoryStream.ToArray();

                                    header.Name = ReaderOptions
                                        .ArchiveEncoding.Decode(bytes)
                                        .TrimNulls();
                                }

                                stream.Position = oldStreamPos;
                            }
                            else
                            {
                                // Streaming mode: PackedStream is already a buffered MemoryStream
                                using var entryStream = entry.OpenEntryStream();
                                using var memoryStream = new MemoryStream();
                                await entryStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                                var bytes = memoryStream.ToArray();
                                header.Name = ReaderOptions
                                    .ArchiveEncoding.Decode(bytes)
                                    .TrimNulls();
                            }

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
