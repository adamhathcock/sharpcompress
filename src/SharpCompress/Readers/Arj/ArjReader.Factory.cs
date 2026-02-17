#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.Arj;

public partial class ArjReader : IReaderOpenable<IArjReader, IArjAsyncReader>
{
    public static ValueTask<IArjAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IArjAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IArjAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IArjAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IArjAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IArjAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IArjReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IArjReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
#endif
