using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Polyfills;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
{
    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task<TarArchive> OpenAsync(
        string filePath,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return await OpenAsync(new FileInfo(filePath), readerOptions ?? new ReaderOptions(), cancellationToken);
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new TarArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task<TarArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        var archive = new TarArchive();
        archive.SourceStream = new SourceStream(
            fileInfo,
            i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
            readerOptions ?? new ReaderOptions()
        );
        archive.LazyVolumes = new Lazy<IReadOnlyCollection<TarVolume>>(() => archive.LoadVolumes(archive.SourceStream).ToList());
        archive.LazyEntries = new Lazy<IReadOnlyCollection<TarArchiveEntry>>(
            () => archive.LoadEntriesAsync(archive.Volumes, cancellationToken).GetAwaiter().GetResult().ToList()
        );
        return archive;
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new TarArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task<TarArchive> OpenAsync(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        var archive = new TarArchive();
        archive.SourceStream = new SourceStream(
            files[0],
            i => i < files.Length ? files[i] : null,
            readerOptions ?? new ReaderOptions()
        );
        archive.LazyVolumes = new Lazy<IReadOnlyCollection<TarVolume>>(() => archive.LoadVolumes(archive.SourceStream).ToList());
        archive.LazyEntries = new Lazy<IReadOnlyCollection<TarArchiveEntry>>(
            () => archive.LoadEntriesAsync(archive.Volumes, cancellationToken).GetAwaiter().GetResult().ToList()
        );
        return archive;
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new TarArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task<TarArchive> OpenAsync(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        var archive = new TarArchive();
        archive.SourceStream = new SourceStream(
            strms[0],
            i => i < strms.Length ? strms[i] : null,
            readerOptions ?? new ReaderOptions()
        );
        archive.LazyVolumes = new Lazy<IReadOnlyCollection<TarVolume>>(() => archive.LoadVolumes(archive.SourceStream).ToList());
        archive.LazyEntries = new Lazy<IReadOnlyCollection<TarArchiveEntry>>(
            () => archive.LoadEntriesAsync(archive.Volumes, cancellationToken).GetAwaiter().GetResult().ToList()
        );
        return archive;
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new TarArchive(
            new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions())
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static async Task<TarArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        var archive = new TarArchive();
        archive.SourceStream = new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions());
        archive.LazyVolumes = new Lazy<IReadOnlyCollection<TarVolume>>(() => archive.LoadVolumes(archive.SourceStream).ToList());
        archive.LazyEntries = new Lazy<IReadOnlyCollection<TarArchiveEntry>>(
            () => archive.LoadEntriesAsync(archive.Volumes, cancellationToken).GetAwaiter().GetResult().ToList()
        );
        return archive;
    }

    public static bool IsTarFile(string filePath) => IsTarFile(new FileInfo(filePath));

    public static bool IsTarFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsTarFile(stream);
    }

    public static async Task<bool> IsTarFileAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => await IsTarFileAsync(new FileInfo(filePath), cancellationToken);

    public static async Task<bool> IsTarFileAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return await IsTarFileAsync(stream, cancellationToken);
    }

    public static bool IsTarFile(Stream stream)
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            var readSucceeded = tarHeader.Read(new BinaryReader(stream));
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch { }
        return false;
    }

    public static async Task<bool> IsTarFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            var readSucceeded = await tarHeader.ReadAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch { }
        return false;
    }

    protected override IEnumerable<TarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts(); //request all streams
        return new TarVolume(sourceStream, ReaderOptions, 1).AsEnumerable(); //simple single volume or split, multivolume not supported
    }

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    private TarArchive(SourceStream sourceStream)
        : base(ArchiveType.Tar, sourceStream) { }

    private TarArchive()
        : base(ArchiveType.Tar)
    {
        LazyVolumes = new Lazy<IReadOnlyCollection<TarVolume>>(() => LoadVolumes(SourceStream!).ToList());
        LazyEntries = new Lazy<IReadOnlyCollection<TarArchiveEntry>>(() => LoadEntries(Volumes).ToList());
    }

    protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        TarHeader? previousHeader = null;
        foreach (
            var header in TarHeaderFactory.ReadHeader(
                StreamingMode.Seekable,
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
                            new TarFilePart(previousHeader, stream),
                            CompressionType.None
                        );

                        var oldStreamPos = stream.Position;

                        using (var entryStream = entry.OpenEntryStream())
                        {
                            using var memoryStream = new MemoryStream();
                            entryStream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                            var bytes = memoryStream.ToArray();

                            header.Name = ReaderOptions.ArchiveEncoding.Decode(bytes).TrimNulls();
                        }

                        stream.Position = oldStreamPos;

                        previousHeader = null;
                    }
                    yield return new TarArchiveEntry(
                        this,
                        new TarFilePart(header, stream),
                        CompressionType.None
                    );
                }
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
    }

    private async Task<IReadOnlyCollection<TarArchiveEntry>> LoadEntriesAsync(IEnumerable<TarVolume> volumes, CancellationToken cancellationToken)
    {
        var list = new List<TarArchiveEntry>();
        var stream = volumes.Single().Stream;
        TarHeader? previousHeader = null;
        await foreach (
            var header in TarHeaderFactory.ReadHeaderAsync(
                StreamingMode.Seekable,
                stream,
                ReaderOptions.ArchiveEncoding,
                cancellationToken
            ).WithCancellation(cancellationToken)
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
                            new TarFilePart(previousHeader, stream),
                            CompressionType.None
                        );

                        var oldStreamPos = stream.Position;

                        using (var entryStream = entry.OpenEntryStream())
                        {
                            using var memoryStream = new MemoryStream();
                            await entryStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
                            memoryStream.Position = 0;
                            var bytes = memoryStream.ToArray();

                            header.Name = ReaderOptions.ArchiveEncoding.Decode(bytes).TrimNulls();
                        }

                        stream.Position = oldStreamPos;

                        previousHeader = null;
                    }
                    list.Add(new TarArchiveEntry(
                        this,
                        new TarFilePart(header, stream),
                        CompressionType.None
                    ));
                }
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
        return list.AsReadOnly();
    }

    public static TarArchive Create() => new();

    protected override TarArchiveEntry CreateEntryInternal(
        string filePath,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    ) =>
        new TarWritableArchiveEntry(
            this,
            source,
            CompressionType.Unknown,
            filePath,
            size,
            modified,
            closeStream
        );

    protected override TarArchiveEntry CreateDirectoryEntry(
        string directoryPath,
        DateTime? modified
    ) => new TarWritableArchiveEntry(this, directoryPath, modified);

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries
    )
    {
        using var writer = new TarWriter(stream, new TarWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries))
        {
            if (entry.IsDirectory)
            {
                writer.WriteDirectory(
                    entry.Key.NotNull("Entry Key is null"),
                    entry.LastModifiedTime
                );
            }
            else
            {
                using var entryStream = entry.OpenEntryStream();
                writer.Write(
                    entry.Key.NotNull("Entry Key is null"),
                    entryStream,
                    entry.LastModifiedTime,
                    entry.Size
                );
            }
        }
    }

    protected override async Task SaveToAsync(
        Stream stream,
        WriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries,
        CancellationToken cancellationToken = default
    )
    {
        using var writer = new TarWriter(stream, new TarWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries))
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

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return TarReader.Open(stream);
    }
}
