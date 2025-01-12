using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Iso;

namespace SharpCompress.Archives.Iso;

public class IsoArchive : AbstractWritableArchive<IsoArchiveEntry, IsoVolume>
{
    public static IsoArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    public static IsoArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return new IsoArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IsoArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new IsoArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IsoArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.CheckNotNull(nameof(streams));
        var strms = streams.ToArray();
        return new IsoArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IsoArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new IsoArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static IsoArchive Create() => new();

    private IsoArchive(SourceStream sourceStream)
        : base(ArchiveType.Iso, sourceStream) { }

    protected override IEnumerable<IsoVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        return sourceStream.Streams.Select(a => new IsoVolume(a, ReaderOptions, 0));
    }

    public void SaveTo(string filePath) => SaveTo(new FileInfo(filePath));

    public void SaveTo(FileInfo fileInfo)
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        SaveTo(stream, new WriterOptions(CompressionType.None));
    }

    internal IsoArchive()
        : base(ArchiveType.Iso) { }

    protected override IsoArchiveEntry CreateEntryInternal(
        string filePath,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    )
    {
        return new IsoWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
    }

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<IsoArchiveEntry> oldEntries,
        IEnumerable<IsoArchiveEntry> newEntries
    )
    {
        using var writer = new IsoWriter(stream, options);
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

    protected override IEnumerable<IsoArchiveEntry> LoadEntries(IEnumerable<IsoVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        yield return new IsoArchiveEntry(
            this,
            new IsoFilePart(stream, ReaderOptions.ArchiveEncoding)
        );
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return IsoReader.Open(stream);
    }
}
