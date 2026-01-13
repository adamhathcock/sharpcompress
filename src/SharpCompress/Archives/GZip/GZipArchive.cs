using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

public partial class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
{
    private GZipArchive(SourceStream sourceStream)
        : base(ArchiveType.GZip, sourceStream) { }

    internal GZipArchive()
        : base(ArchiveType.GZip) { }

    protected override IEnumerable<GZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        return sourceStream.Streams.Select(a => new GZipVolume(a, ReaderOptions, 0));
    }

    public void SaveTo(string filePath) => SaveTo(new FileInfo(filePath));

    public void SaveTo(FileInfo fileInfo)
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        SaveTo(stream, new WriterOptions(CompressionType.GZip));
    }

    public ValueTask SaveToAsync(string filePath, CancellationToken cancellationToken = default) =>
        SaveToAsync(new FileInfo(filePath), cancellationToken);

    public async ValueTask SaveToAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        await SaveToAsync(stream, new WriterOptions(CompressionType.GZip), cancellationToken)
            .ConfigureAwait(false);
    }

    protected override GZipArchiveEntry CreateEntryInternal(
        string filePath,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    )
    {
        if (Entries.Any())
        {
            throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
        }
        return new GZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
    }

    protected override GZipArchiveEntry CreateDirectoryEntry(
        string directoryPath,
        DateTime? modified
    ) => throw new NotSupportedException("GZip archives do not support directory entries.");

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<GZipArchiveEntry> oldEntries,
        IEnumerable<GZipArchiveEntry> newEntries
    )
    {
        if (Entries.Count > 1)
        {
            throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
        }
        using var writer = new GZipWriter(stream, new GZipWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries).Where(x => !x.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            writer.Write(
                entry.Key.NotNull("Entry Key is null"),
                entryStream,
                entry.LastModifiedTime
            );
        }
    }

    protected override async ValueTask SaveToAsync(
        Stream stream,
        WriterOptions options,
        IEnumerable<GZipArchiveEntry> oldEntries,
        IEnumerable<GZipArchiveEntry> newEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (Entries.Count > 1)
        {
            throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
        }
        using var writer = new GZipWriter(stream, new GZipWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries).Where(x => !x.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            await writer
                .WriteAsync(entry.Key.NotNull("Entry Key is null"), entryStream, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    protected override IEnumerable<GZipArchiveEntry> LoadEntries(IEnumerable<GZipVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        yield return new GZipArchiveEntry(
            this,
             GZipFilePart.Create(stream, ReaderOptions.ArchiveEncoding)
        );
    }

    protected override async IAsyncEnumerable<GZipArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<GZipVolume> volumes
    )
    {
        var stream = (await volumes.SingleAsync()).Stream;
        yield return new GZipArchiveEntry(
            this,
            await GZipFilePart.CreateAsync(stream, ReaderOptions.ArchiveEncoding)
        );
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return GZipReader.Open(stream);
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return new((IAsyncReader)GZipReader.Open(stream));
    }
}
