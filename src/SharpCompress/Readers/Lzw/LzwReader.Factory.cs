using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers.Lzw;

public partial class LzwReader
#if NET8_0_OR_GREATER
    : IReaderOpenable<ILzwReader, ILzwAsyncReader>
#endif
{
    public static ValueTask<ILzwAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((ILzwAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<ILzwAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((ILzwAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<ILzwAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((ILzwAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static ILzwReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static ILzwReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }

    public static ILzwReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new LzwReader(stream, readerOptions ?? new ReaderOptions());
    }
}
