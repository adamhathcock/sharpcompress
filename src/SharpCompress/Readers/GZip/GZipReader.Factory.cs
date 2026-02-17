using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader
#if NET8_0_OR_GREATER
    : IReaderOpenable<IGZipReader, IGZipAsyncReader>
#endif
{
    public static ValueTask<IGZipAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IGZipAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IGZipAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IGZipAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IGZipAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IGZipAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IGZipReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IGZipReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }

    public static IGZipReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new GZipReader(stream, readerOptions ?? new ReaderOptions());
    }
}
