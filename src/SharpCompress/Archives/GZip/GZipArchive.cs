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

public class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
{
    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static IArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
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
    public static IArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
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
    public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.NotNull(nameof(streams));
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
    public static IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new GZipArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

    /// <summary>
    /// Opens a GZipArchive asynchronously from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IAsyncArchive OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(stream, readerOptions);
    }

    /// <summary>
    /// Opens a GZipArchive asynchronously from a FileInfo.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfo, readerOptions);
    }

    /// <summary>
    /// Opens a GZipArchive asynchronously from multiple streams.
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(streams, readerOptions);
    }

    /// <summary>
    /// Opens a GZipArchive asynchronously from multiple FileInfo objects.
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfos, readerOptions);
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

    public static async ValueTask<bool> IsGZipFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        // read the header on the first read
        byte[] header = new byte[10];

        // workitem 8501: handle edge case (decompress empty stream)
        if (!await stream.ReadFullyAsync(header, cancellationToken).ConfigureAwait(false))
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
            new GZipFilePart(stream, ReaderOptions.ArchiveEncoding)
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
