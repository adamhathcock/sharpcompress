using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;

namespace SharpCompress.Readers.Zip;

public partial class ZipReader
#if NET8_0_OR_GREATER
    : IReaderOpenable<IZipReader, IZipAsyncReader>
#endif
{
    /// <summary>
    /// Opens a ZipReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IZipReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, readerOptions ?? new ReaderOptions());
    }

    public static IZipReader OpenReader(
        Stream stream,
        ReaderOptions? options,
        IEnumerable<ZipEntry> entries
    )
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, options ?? new ReaderOptions(), entries);
    }

    public static ValueTask<IZipAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IZipAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IZipAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IZipAsyncReader)OpenReader(stream, readerOptions));
    }

    public static ValueTask<IZipAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IZipAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IZipReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IZipReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
