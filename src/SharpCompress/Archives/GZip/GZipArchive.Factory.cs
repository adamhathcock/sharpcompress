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

public partial class GZipArchive
{
    public static IArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

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

    public static IWritableArchive Open(Stream stream, ReaderOptions? readerOptions = null)
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

    public static IWritableAsyncArchive OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)Open(stream, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)Open(fileInfo, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)Open(streams, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)Open(fileInfos, readerOptions);
    }

    public static GZipArchive Create() => new();

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

    public static bool IsGZipFile(Stream stream)
    {
        Span<byte> header = stackalloc byte[10];

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
        byte[] header = new byte[10];

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
}
