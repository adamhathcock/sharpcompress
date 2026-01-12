using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Archives.Rar;

public interface IRarArchiveCommon
{


    int MinVersion { get; }
    int MaxVersion { get; }
}

public interface IRarArchive : IArchive, IRarArchiveCommon
{
}

public interface IRarAsyncArchive : IAsyncArchive, IRarArchiveCommon
{
}

public class RarArchive : AbstractArchive<RarArchiveEntry, RarVolume>, IRarArchive
{
    private bool _disposed;
    internal Lazy<IRarUnpack> UnpackV2017 { get; } =
        new(() => new Compressors.Rar.UnpackV2017.Unpack());
    internal Lazy<IRarUnpack> UnpackV1 { get; } = new(() => new Compressors.Rar.UnpackV1.Unpack());

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    private RarArchive(SourceStream sourceStream)
        : base(ArchiveType.Rar, sourceStream) { }

    public override void Dispose()
    {
        if (!_disposed)
        {
            if (UnpackV1.IsValueCreated && UnpackV1.Value is IDisposable unpackV1)
            {
                unpackV1.Dispose();
            }

            _disposed = true;
            base.Dispose();
        }
    }

    protected override IEnumerable<RarArchiveEntry> LoadEntries(IEnumerable<RarVolume> volumes) =>
        RarArchiveEntryFactory.GetEntries(this, volumes, ReaderOptions);

    protected override IEnumerable<RarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts(); //request all streams
        var streams = sourceStream.Streams.ToArray();
        var i = 0;
        if (streams.Length > 1 && IsRarFile(streams[1], ReaderOptions)) //test part 2 - true = multipart not split
        {
            sourceStream.IsVolumes = true;
            streams[1].Position = 0;
            sourceStream.Position = 0;

            return sourceStream.Streams.Select(a => new StreamRarArchiveVolume(
                a,
                ReaderOptions,
                i++
            ));
        }

        //split mode or single file
        return new StreamRarArchiveVolume(sourceStream, ReaderOptions, i++).AsEnumerable();
    }

    protected override IReader CreateReaderForSolidExtraction() =>
        CreateReaderForSolidExtractionInternal();

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync() =>
        new(CreateReaderForSolidExtractionInternal());

    private RarReader CreateReaderForSolidExtractionInternal()
    {
        if (this.IsMultipartVolume())
        {
            var streams = Volumes.Select(volume =>
            {
                volume.Stream.Position = 0;
                return volume.Stream;
            });
            return (RarReader)RarReader.Open(streams, ReaderOptions);
        }

        var stream = Volumes.First().Stream;
        stream.Position = 0;
        return (RarReader)RarReader.Open(stream, ReaderOptions);
    }

    public override bool IsSolid => Volumes.First().IsSolidArchive;

    public override bool IsEncrypted => Entries.First(x => !x.IsDirectory).IsEncrypted;

    public virtual int MinVersion => Volumes.First().MinVersion;
    public virtual int MaxVersion => Volumes.First().MaxVersion;

    #region Creation
    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    public static IRarArchive Open(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        var fileInfo = new FileInfo(filePath);
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="options"></param>
    public static IRarArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    public static IRarArchive Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new RarArchive(new SourceStream(stream, _ => null, options ?? new ReaderOptions()));
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static IRarArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new RarArchive(
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
    public static IRarArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new RarArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Opens a RarArchive asynchronously from a stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IRarAsyncArchive OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(stream, readerOptions);
    }

    /// <summary>
    /// Opens a RarArchive asynchronously from a FileInfo.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IRarAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(fileInfo, readerOptions);
    }

    /// <summary>
    /// Opens a RarArchive asynchronously from multiple streams.
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IRarAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(streams, readerOptions);
    }

    /// <summary>
    /// Opens a RarArchive asynchronously from multiple FileInfo objects.
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    /// <param name="cancellationToken"></param>
    public static IRarAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(fileInfos, readerOptions);
    }

    public static bool IsRarFile(string filePath) => IsRarFile(new FileInfo(filePath));

    public static bool IsRarFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsRarFile(stream);
    }

    public static bool IsRarFile(Stream stream, ReaderOptions? options = null)
    {
        try
        {
            MarkHeader.Read(stream, true, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
