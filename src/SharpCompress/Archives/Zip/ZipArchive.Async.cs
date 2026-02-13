using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchive
{
    protected override async IAsyncEnumerable<ZipArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<ZipVolume> volumes
    )
    {
        var vols = await volumes.ToListAsync().ConfigureAwait(false);
        var volsArray = vols.ToArray();

        await foreach (
            var h in headerFactory.NotNull().ReadSeekableHeaderAsync(volsArray.Last().Stream)
        )
        {
            if (h != null)
            {
                switch (h.ZipHeaderType)
                {
                    case ZipHeaderType.DirectoryEntry:
                        {
                            var deh = (DirectoryEntryHeader)h;
                            Stream s;
                            if (
                                deh.RelativeOffsetOfEntryHeader + deh.CompressedSize
                                > volsArray[deh.DiskNumberStart].Stream.Length
                            )
                            {
                                var v = volsArray.Skip(deh.DiskNumberStart).ToArray();
                                s = new SourceStream(
                                    v[0].Stream,
                                    i => i < v.Length ? v[i].Stream : null,
                                    new ReaderOptions() { LeaveStreamOpen = true }
                                );
                            }
                            else
                            {
                                s = volsArray[deh.DiskNumberStart].Stream;
                            }

                            yield return new ZipArchiveEntry(
                                this,
                                new SeekableZipFilePart(
                                    headerFactory.NotNull(),
                                    deh,
                                    s,
                                    ReaderOptions.Providers
                                ),
                                ReaderOptions
                            );
                        }
                        break;
                    case ZipHeaderType.DirectoryEnd:
                    {
                        var bytes = ((DirectoryEndHeader)h).Comment ?? Array.Empty<byte>();
                        volsArray.Last().Comment = ReaderOptions.ArchiveEncoding.Decode(bytes);
                        yield break;
                    }
                }
            }
        }
    }

    protected override async ValueTask SaveToAsync(
        Stream stream,
        ZipWriterOptions options,
        IAsyncEnumerable<ZipArchiveEntry> oldEntries,
        IEnumerable<ZipArchiveEntry> newEntries,
        CancellationToken cancellationToken = default
    )
    {
        using var writer = new ZipWriter(stream, options);
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
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
    }
}
