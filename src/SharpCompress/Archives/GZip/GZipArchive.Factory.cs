using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

public partial class GZipArchive
#if NET8_0_OR_GREATER
    : IWritableArchiveOpenable<GZipWriterOptions>,
        IMultiArchiveOpenable<
            IWritableArchive<GZipWriterOptions>,
            IWritableAsyncArchive<GZipWriterOptions>
        >
#endif
{
    public static IWritableAsyncArchive<GZipWriterOptions> OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return (IWritableAsyncArchive<GZipWriterOptions>)
            OpenArchive(new FileInfo(path), readerOptions ?? new ReaderOptions());
    }

    public static IWritableArchive<GZipWriterOptions> OpenArchive(
        string filePath,
        ReaderOptions? readerOptions = null
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    public static IWritableArchive<GZipWriterOptions> OpenArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    )
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

    public static IWritableArchive<GZipWriterOptions> OpenArchive(
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

    public static IWritableArchive<GZipWriterOptions> OpenArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
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

    public static IWritableArchive<GZipWriterOptions> OpenArchive(
        Stream stream,
        ReaderOptions? readerOptions = null
    )
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

    public static IWritableAsyncArchive<GZipWriterOptions> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive<GZipWriterOptions>)OpenArchive(stream, readerOptions);
    }

    public static IWritableAsyncArchive<GZipWriterOptions> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive<GZipWriterOptions>)OpenArchive(fileInfo, readerOptions);
    }

    public static IWritableAsyncArchive<GZipWriterOptions> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive<GZipWriterOptions>)OpenArchive(streams, readerOptions);
    }

    public static IWritableAsyncArchive<GZipWriterOptions> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive<GZipWriterOptions>)OpenArchive(fileInfos, readerOptions);
    }

    public static IWritableArchive<GZipWriterOptions> CreateArchive() => new GZipArchive();

    public static IWritableAsyncArchive<GZipWriterOptions> CreateAsyncArchive() =>
        new GZipArchive();

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

    private readonly struct GZipSignatureTryProcessor : Utility.ITryBufferProcessor<(bool, bool)>
    {
        public (bool, bool) OnSuccess(ReadOnlySpan<byte> buffer) => (true, buffer[0] == 0x1F && buffer[1] == 0x8B && buffer[2] == 8);
        public (bool, bool) OnFailure() => (false, false);
    }

    public static async ValueTask<bool> IsGZipFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var processor = new GZipSignatureTryProcessor();
        var result = await stream.TryReadFullyRentedAsync<GZipSignatureTryProcessor, (bool success, bool match)>(
            10,
            processor,
            cancellationToken
        );
        return result.success && result.match;
    }
}
