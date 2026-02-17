#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.Arc;

public partial class ArcReader : IReaderOpenable<IArcReader, IArcAsyncReader>
{
    public static ValueTask<IArcAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IArcAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IArcAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IArcAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IArcAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IArcAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IArcReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IArcReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
#endif
