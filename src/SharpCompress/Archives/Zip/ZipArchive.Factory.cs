using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchive
#if NET8_0_OR_GREATER
    : IWritableArchiveOpenable,
        IMultiArchiveOpenable<IWritableArchive, IWritableAsyncArchive>
#endif
{
    public static IWritableArchive OpenArchive(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), readerOptions);
    }

    public static IWritableArchive OpenArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new ZipArchive(
            new SourceStream(
                fileInfo,
                i => ZipArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions() { LeaveStreamOpen = false }
            )
        );
    }

    public static IWritableArchive OpenArchive(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new ZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions() { LeaveStreamOpen = false }
            )
        );
    }

    public static IWritableArchive OpenArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new ZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IWritableArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new ZipArchive(
            new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static IWritableAsyncArchive OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(path, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(stream, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(fileInfo, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(streams, readerOptions);
    }

    public static IWritableAsyncArchive OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(fileInfos, readerOptions);
    }

    public static bool IsZipFile(
        string filePath,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => IsZipFile(new FileInfo(filePath), password, bufferSize);

    public static bool IsZipFile(
        FileInfo fileInfo,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsZipFile(stream, password, bufferSize);
    }

    public static bool IsZipFile(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            if (stream is not SharpCompressStream)
            {
                stream = new SharpCompressStream(stream, bufferSize: bufferSize);
            }

            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                return false;
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsZipMulti(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            if (stream is not SharpCompressStream)
            {
                stream = new SharpCompressStream(stream, bufferSize: bufferSize);
            }

            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                if (stream.CanSeek)
                {
                    var z = new SeekableZipHeaderFactory(password, new ArchiveEncoding());
                    var x = z.ReadSeekableHeader(stream).FirstOrDefault();
                    return x?.ZipHeaderType == ZipHeaderType.DirectoryEntry;
                }
                else
                {
                    return false;
                }
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async ValueTask<bool> IsZipFileAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            if (stream is not SharpCompressStream)
            {
                stream = new SharpCompressStream(stream, bufferSize: bufferSize);
            }

            var header = await headerFactory
                .ReadStreamHeaderAsync(stream)
                .Where(x => x.ZipHeaderType != ZipHeaderType.Split)
                .FirstOrDefaultAsync(cancellationToken);
            if (header is null)
            {
                return false;
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IWritableArchive CreateArchive() => new ZipArchive();

    public static IWritableAsyncArchive CreateAsyncArchive() => new ZipArchive();

    public static async ValueTask<bool> IsZipMultiAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            if (stream is not SharpCompressStream)
            {
                stream = new SharpCompressStream(stream, bufferSize: bufferSize);
            }

            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                if (stream.CanSeek)
                {
                    var z = new SeekableZipHeaderFactory(password, new ArchiveEncoding());
                    ZipHeader? x = null;
                    await foreach (
                        var h in z.ReadSeekableHeaderAsync(stream)
                            .WithCancellation(cancellationToken)
                    )
                    {
                        x = h;
                        break;
                    }
                    return x?.ZipHeaderType == ZipHeaderType.DirectoryEntry;
                }
                else
                {
                    return false;
                }
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
