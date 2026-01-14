#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader : IReaderOpenable
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

    public static IReader Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions);
    }

    public static IReader Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return Open(fileInfo.OpenRead(), readerOptions);
    }
}
#endif
