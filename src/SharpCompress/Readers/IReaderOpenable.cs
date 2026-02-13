#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers;

public interface IReaderOpenable
{
    public static abstract IReader OpenReader(string filePath, ReaderOptions? readerOptions = null);

    public static abstract IReader OpenReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    );

    public static abstract IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null);

    public static abstract ValueTask<IAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );
}
#endif
