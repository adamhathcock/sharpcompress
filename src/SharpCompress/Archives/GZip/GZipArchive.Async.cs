using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Common.Options;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

public partial class GZipArchive
{
    public ValueTask SaveToAsync(string filePath, CancellationToken cancellationToken = default) =>
        SaveToAsync(new FileInfo(filePath), cancellationToken);

    public async ValueTask SaveToAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        await SaveToAsync(stream, new GZipWriterOptions(CompressionType.GZip), cancellationToken)
            .ConfigureAwait(false);
    }

    protected override async ValueTask SaveToAsync(
        Stream stream,
        GZipWriterOptions options,
        IAsyncEnumerable<GZipArchiveEntry> oldEntries,
        IEnumerable<GZipArchiveEntry> newEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (Entries.Count > 1)
        {
            throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
        }
        await using var writer = new GZipWriter(stream, options);
        await foreach (
            var entry in oldEntries.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            if (!entry.IsDirectory)
            {
                using var entryStream = await entry
                    .OpenEntryStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                await writer
                    .WriteAsync(
                        entry.Key.NotNull("Entry Key is null"),
                        entryStream,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
        foreach (var entry in newEntries.Where(x => !x.IsDirectory))
        {
            using var entryStream = await entry
                .OpenEntryStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await writer
                .WriteAsync(entry.Key.NotNull("Entry Key is null"), entryStream, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return new((IAsyncReader)GZipReader.OpenReader(stream, ReaderOptions));
    }

    protected override async IAsyncEnumerable<GZipArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<GZipVolume> volumes
    )
    {
        var stream = (await volumes.SingleAsync().ConfigureAwait(false)).Stream;
        yield return new GZipArchiveEntry(
            this,
            await GZipFilePart
                .CreateAsync(stream, ReaderOptions.ArchiveEncoding, ReaderOptions.Providers)
                .ConfigureAwait(false),
            ReaderOptions
        );
    }
}
