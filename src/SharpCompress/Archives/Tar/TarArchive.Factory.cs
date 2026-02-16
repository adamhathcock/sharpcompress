using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive
#if NET8_0_OR_GREATER
    : IWritableArchiveOpenable<TarWriterOptions>,
        IMultiArchiveOpenable<
            IWritableArchive<TarWriterOptions>,
            IWritableAsyncArchive<TarWriterOptions>
        >
#endif
{
    public static IWritableArchive<TarWriterOptions> OpenArchive(
        string filePath,
        ReaderOptions? readerOptions = null
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), readerOptions);
    }

    public static IWritableArchive<TarWriterOptions> OpenArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenArchive(
            [fileInfo],
            readerOptions ?? new ReaderOptions() { LeaveStreamOpen = false }
        );
    }

    public static IWritableArchive<TarWriterOptions> OpenArchive(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        var sourceStream = new SourceStream(
            files[0],
            i => i < files.Length ? files[i] : null,
            readerOptions ?? new ReaderOptions() { LeaveStreamOpen = false }
        );
        var compressionType = TarFactory.GetCompressionType(
            sourceStream,
            sourceStream.ReaderOptions.Providers
        );
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
    }

    public static IWritableArchive<TarWriterOptions> OpenArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        var sourceStream = new SourceStream(
            strms[0],
            i => i < strms.Length ? strms[i] : null,
            readerOptions ?? new ReaderOptions()
        );
        var compressionType = TarFactory.GetCompressionType(
            sourceStream,
            sourceStream.ReaderOptions.Providers
        );
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
    }

    public static IWritableArchive<TarWriterOptions> OpenArchive(
        Stream stream,
        ReaderOptions? readerOptions = null
    )
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return OpenArchive([stream], readerOptions);
    }

    public static async ValueTask<IWritableAsyncArchive<TarWriterOptions>> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.NotNull(nameof(stream));
        var sourceStream = new SourceStream(
            stream,
            i => null,
            readerOptions ?? new ReaderOptions()
        );
        var compressionType = await TarFactory
            .GetCompressionTypeAsync(
                sourceStream,
                sourceStream.ReaderOptions.Providers,
                cancellationToken
            )
            .ConfigureAwait(false);
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
    }

    public static ValueTask<IWritableAsyncArchive<TarWriterOptions>> OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return OpenAsyncArchive(new FileInfo(path), readerOptions, cancellationToken);
    }

    public static async ValueTask<IWritableAsyncArchive<TarWriterOptions>> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        fileInfo.NotNull(nameof(fileInfo));
        readerOptions ??= new ReaderOptions() { LeaveStreamOpen = false };
        var sourceStream = new SourceStream(fileInfo, i => null, readerOptions);
        var compressionType = await TarFactory
            .GetCompressionTypeAsync(
                sourceStream,
                sourceStream.ReaderOptions.Providers,
                cancellationToken
            )
            .ConfigureAwait(false);
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
    }

    public static async ValueTask<IWritableAsyncArchive<TarWriterOptions>> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        var sourceStream = new SourceStream(
            strms[0],
            i => i < strms.Length ? strms[i] : null,
            readerOptions ?? new ReaderOptions()
        );
        var compressionType = await TarFactory
            .GetCompressionTypeAsync(
                sourceStream,
                sourceStream.ReaderOptions.Providers,
                cancellationToken
            )
            .ConfigureAwait(false);
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
    }

    public static async ValueTask<IWritableAsyncArchive<TarWriterOptions>> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        var sourceStream = new SourceStream(
            files[0],
            i => i < files.Length ? files[i] : null,
            readerOptions ?? new ReaderOptions() { LeaveStreamOpen = false }
        );
        var compressionType = await TarFactory
            .GetCompressionTypeAsync(
                sourceStream,
                sourceStream.ReaderOptions.Providers,
                cancellationToken
            )
            .ConfigureAwait(false);
        sourceStream.Seek(0, SeekOrigin.Begin);
        return new TarArchive(sourceStream, compressionType);
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
                && IsDefined(tarHeader.EntryType);
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
#if NET8_0_OR_GREATER
            await using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#else
            using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#endif
            var readSucceeded = await tarHeader.ReadAsync(reader).ConfigureAwait(false);
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && IsDefined(tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch (Exception)
        {
            // Catch all exceptions during tar header reading to determine if this is a valid tar file
            // Invalid tar files or corrupted streams will throw various exceptions
            return false;
        }
    }

    public static IWritableArchive<TarWriterOptions> CreateArchive() => new TarArchive();

    public static ValueTask<IWritableAsyncArchive<TarWriterOptions>> CreateAsyncArchive() =>
        new(new TarArchive());

    private static bool IsDefined(EntryType value)
    {
#if LEGACY_DOTNET
        return Enum.IsDefined(typeof(EntryType), value);
#else
        return Enum.IsDefined(value);
#endif
    }
}
