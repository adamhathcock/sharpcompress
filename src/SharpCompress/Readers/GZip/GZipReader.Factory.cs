using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader
#if NET8_0_OR_GREATER
    : IReaderOpenable
#endif
{
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        filePath.NotNullOrEmpty(nameof(filePath));
        return new((IAsyncReader)OpenReader(new FileInfo(filePath), readerOptions));
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        readerOptions ??= ReaderOptions.ForFilePath;
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }

    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.RequireReadable();
        return new GZipReader(stream, readerOptions ?? ReaderOptions.ForExternalStream);
    }
}
