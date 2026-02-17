#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.Rar;

public partial class RarReader : IReaderOpenable<IRarReader, IRarAsyncReader>
{
    public static ValueTask<IRarAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IRarAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IRarAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IRarAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IRarAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IRarAsyncReader)OpenReader(fileInfo, readerOptions));
    }
}
#endif
