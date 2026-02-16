using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

public partial class SevenZipArchive
#if NET8_0_OR_GREATER
    : IArchiveOpenable<IArchive, IAsyncArchive>,
        IMultiArchiveOpenable<IArchive, IAsyncArchive>
#endif
{
    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty("path");
        return new(
            (IAsyncArchive)OpenArchive(new FileInfo(path), readerOptions ?? new ReaderOptions())
        );
    }

    public static IArchive OpenArchive(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty("filePath");
        return OpenArchive(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    public static IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new SevenZipArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive OpenArchive(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive OpenArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new SevenZipArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(stream, readerOptions));
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(fileInfo, readerOptions));
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(streams, readerOptions));
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(fileInfos, readerOptions));
    }

    public static bool IsSevenZipFile(string filePath) => IsSevenZipFile(new FileInfo(filePath));

    public static bool IsSevenZipFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsSevenZipFile(stream);
    }

    public static bool IsSevenZipFile(Stream stream)
    {
        try
        {
            return SignatureMatch(stream);
        }
        catch
        {
            return false;
        }
    }

    public static async ValueTask<bool> IsSevenZipFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await SignatureMatchAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static ReadOnlySpan<byte> Signature => [(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C];

    private static bool SignatureMatch(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(6);
        try
        {
            stream.ReadExact(buffer, 0, 6);
            return buffer.AsSpan().Slice(0, 6).SequenceEqual(Signature);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask<bool> SignatureMatchAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(6);
        try
        {
            if (!await stream.ReadFullyAsync(buffer, 0, 6, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return buffer.AsSpan().Slice(0, 6).SequenceEqual(Signature);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
