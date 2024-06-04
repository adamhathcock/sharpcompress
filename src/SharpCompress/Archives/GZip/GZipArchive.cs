using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

public class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
{
    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static GZipArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static GZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return new GZipArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static GZipArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new GZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    public static GZipArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.CheckNotNull(nameof(streams));
        var strms = streams.ToArray();
        return new GZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    public static GZipArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new GZipArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static GZipArchive Create() => new();

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    private GZipArchive(SourceStream sourceStream)
        : base(ArchiveType.GZip, sourceStream) { }

    protected override IEnumerable<GZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        return sourceStream.Streams.Select(a => new GZipVolume(a, ReaderOptions, 0));
    }

    public static bool IsGZipFile(string filePath) => IsGZipFile(new FileInfo(filePath));

    public static bool IsGZipFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }

        using Stream stream = fileInfo.OpenRead();
        return IsGZipFile(stream);
    }

    public void SaveTo(string filePath) => SaveTo(new FileInfo(filePath));

    public void SaveTo(FileInfo fileInfo)
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        SaveTo(stream, new WriterOptions(CompressionType.GZip));
    }

    public static bool IsGZipFile(Stream stream)
    {
        // read the header on the first read
        Span<byte> header = stackalloc byte[10];

        // workitem 8501: handle edge case (decompress empty stream)
        if (!stream.ReadFully(header))
        {
            return false;
        }

        if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
        {
            return false;
        }

        return true;
    }

    internal GZipArchive()
        : base(ArchiveType.GZip) { }

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
            throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
        }
        return new GZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
    }

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<GZipArchiveEntry> oldEntries,
        IEnumerable<GZipArchiveEntry> newEntries
    )
    {
        if (Entries.Count > 1)
        {
            throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
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

    protected override IEnumerable<GZipArchiveEntry> LoadEntries(IEnumerable<GZipVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        yield return new GZipArchiveEntry(
            this,
            new GZipFilePart(stream, ReaderOptions.ArchiveEncoding)
        );
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return GZipReader.Open(stream);
    }
}
