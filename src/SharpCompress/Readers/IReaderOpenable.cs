#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;

namespace SharpCompress.Readers;

public interface IReaderOpenable
{
    public static abstract IReader Open(string filePath, ReaderOptions? readerOptions = null);

    public static abstract IReader Open(FileInfo fileInfo, ReaderOptions? readerOptions = null);

    public static abstract IReader Open(Stream stream, ReaderOptions? readerOptions = null);

    public static abstract IAsyncReader OpenAsync(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract IAsyncReader OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract IAsyncReader OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );
}
#endif
