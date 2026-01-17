using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive
#if NET8_0_OR_GREATER
    : IWritableArchiveOpenable,
        IMultiArchiveOpenable<IWritableArchive, IWritableAsyncArchive>
#endif
{
    public static IWritableArchive OpenArchive(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    public static IWritableArchive OpenArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    )
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

    public static IWritableArchive OpenArchive(
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

    public static IWritableArchive OpenArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
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

    public static IWritableArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
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
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IWritableAsyncArchive)OpenArchive(new FileInfo(path), readerOptions);
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

    public static bool IsTarFile(Stream stream)
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            var reader = new BinaryReader(stream, Encoding.UTF8, false);
            var readSucceeded = tarHeader.Read(reader);
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch (Exception)
        {
            // Catch all exceptions during tar header reading to determine if this is a valid tar file
            // Invalid tar files or corrupted streams will throw various exceptions
            return false;
        }
    }

    public static async ValueTask<bool> IsTarFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            var reader = new AsyncBinaryReader(stream, false);
            var readSucceeded = await tarHeader.ReadAsync(reader);
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch (Exception)
        {
            // Catch all exceptions during tar header reading to determine if this is a valid tar file
            // Invalid tar files or corrupted streams will throw various exceptions
            return false;
        }
    }

    public static IWritableArchive CreateArchive() => new TarArchive();

    public static IWritableAsyncArchive CreateAsyncArchive() => new TarArchive();
}
