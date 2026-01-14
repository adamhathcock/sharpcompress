#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Readers.Rar;

public partial class RarReader : IReaderOpenable
{
    public static IAsyncReader OpenAsync(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return (IAsyncReader)Open(new FileInfo(path), readerOptions);
    }

    public static IAsyncReader OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncReader)Open(stream, readerOptions);
    }

    public static IAsyncReader OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncReader)Open(fileInfo, readerOptions);
    }
}
#endif
