#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers;

public interface IReaderOpenable<out TReader, TAsyncReader>
    where TReader : IReader
    where TAsyncReader : IAsyncReader
{
    public static abstract TReader OpenReader(string filePath, ReaderOptions? readerOptions = null);

    public static abstract TReader OpenReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    );

    public static abstract TReader OpenReader(Stream stream, ReaderOptions? readerOptions = null);

    public static abstract ValueTask<TAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<TAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<TAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );
}
#endif
