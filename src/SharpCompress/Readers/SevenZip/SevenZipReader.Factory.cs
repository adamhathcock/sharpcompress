using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;

namespace SharpCompress.Readers.SevenZip;

public partial class SevenZipReader
#if NET8_0_OR_GREATER
    : IReaderOpenable<ISevenZipReader, ISevenZipAsyncReader>
#endif
{
    /// <summary>
    /// Opens a 7Zip reader from a file path.
    /// </summary>
    public static ISevenZipReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    /// <summary>
    /// Opens a 7Zip reader from a file.
    /// </summary>
    public static ISevenZipReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        var options = readerOptions ?? ReaderOptions.ForOwnedFile;
        return OpenReader(fileInfo.OpenRead(), options);
    }

    /// <summary>
    /// Opens a 7Zip reader from a stream.
    /// </summary>
    public static ISevenZipReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        return new SevenZipReader(
            options,
            (SevenZipArchive)SevenZipArchive.OpenArchive(stream, options),
            disposeArchive: true
        );
    }

    /// <summary>
    /// Opens a 7Zip reader from a file path asynchronously.
    /// </summary>
    public static ValueTask<ISevenZipAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return OpenAsyncReader(new FileInfo(path), readerOptions, cancellationToken);
    }

    /// <summary>
    /// Opens a 7Zip reader from a file asynchronously.
    /// </summary>
    public static ValueTask<ISevenZipAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsyncReader(
            fileInfo.OpenRead(),
            readerOptions ?? ReaderOptions.ForOwnedFile,
            cancellationToken
        );
    }

    /// <summary>
    /// Opens a 7Zip reader from a stream asynchronously.
    /// </summary>
    public static ValueTask<ISevenZipAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((ISevenZipAsyncReader)OpenReader(stream, readerOptions));
    }
}
