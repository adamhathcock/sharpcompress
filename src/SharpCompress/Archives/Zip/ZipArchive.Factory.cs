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
using SharpCompress.Writers.Zip;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchive
#if NET8_0_OR_GREATER
    : IWritableArchiveOpenable<ZipWriterOptions>,
        IMultiArchiveOpenable<
            IWritableArchive<ZipWriterOptions>,
            IWritableAsyncArchive<ZipWriterOptions>
        >
#endif
{
    public static IWritableArchive<ZipWriterOptions> OpenArchive(
        string filePath,
        ReaderOptions? readerOptions = null
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), readerOptions);
    }

    public static IWritableArchive<ZipWriterOptions> OpenArchive(
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

    public static IWritableArchive<ZipWriterOptions> OpenArchive(
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

    public static IWritableArchive<ZipWriterOptions> OpenArchive(
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

    public static IWritableArchive<ZipWriterOptions> OpenArchive(
        Stream stream,
        ReaderOptions? readerOptions = null
    )
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

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IWritableAsyncArchive<ZipWriterOptions>)OpenArchive(path, readerOptions));
    }

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IWritableAsyncArchive<ZipWriterOptions>)OpenArchive(stream, readerOptions));
    }

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IWritableAsyncArchive<ZipWriterOptions>)OpenArchive(fileInfo, readerOptions));
    }

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IWritableAsyncArchive<ZipWriterOptions>)OpenArchive(streams, readerOptions));
    }

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IWritableAsyncArchive<ZipWriterOptions>)OpenArchive(fileInfos, readerOptions));
    }

    public static bool IsZipFile(string filePath, string? password = null) =>
        IsZipFile(new FileInfo(filePath), password);

    public static bool IsZipFile(FileInfo fileInfo, string? password = null)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsZipFile(stream, password);
    }

    public static bool IsZipFile(Stream stream, string? password = null)
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                return false;
            }
            return IsDefined(header.ZipHeaderType);
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

    public static bool IsZipMulti(Stream stream, string? password = null)
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
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
            return IsDefined(header.ZipHeaderType);
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
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = await headerFactory
                .ReadStreamHeaderAsync(stream)
                .Where(x => x.ZipHeaderType != ZipHeaderType.Split)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (header is null)
            {
                return false;
            }
            return IsDefined(header.ZipHeaderType);
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

    public static IWritableArchive<ZipWriterOptions> CreateArchive() => new ZipArchive();

    public static ValueTask<IWritableAsyncArchive<ZipWriterOptions>> CreateAsyncArchive() =>
        new(new ZipArchive());

    public static async ValueTask<bool> IsZipMultiAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = await headerFactory
                .ReadStreamHeaderAsync(stream)
                .Where(x => x.ZipHeaderType != ZipHeaderType.Split)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (header is null)
            {
                if (stream.CanSeek)
                {
                    var z = new SeekableZipHeaderFactory(password, new ArchiveEncoding());
                    ZipHeader? x = null;
                    await foreach (
                        var h in z.ReadSeekableHeaderAsync(stream)
                            .WithCancellation(cancellationToken)
                            .ConfigureAwait(false)
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
            return IsDefined(header.ZipHeaderType);
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

    private static bool IsDefined(ZipHeaderType value)
    {
#if LEGACY_DOTNET
        return Enum.IsDefined(typeof(ZipHeaderType), value);
#else
        return Enum.IsDefined(value);
#endif
    }
}
