using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.Ace;

public partial class AceReader
#if NET8_0_OR_GREATER
    : IReaderOpenable<IAceReader, IAceAsyncReader>
#endif
{
    /// <summary>
    /// Opens an AceReader for non-seeking usage with a single volume.
    /// </summary>
    /// <param name="stream">The stream containing the ACE archive.</param>
    /// <param name="readerOptions">Reader options.</param>
    /// <returns>An AceReader instance.</returns>
    public static IAceReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new SingleVolumeAceReader(stream, readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Opens an AceReader for Non-seeking usage with multiple volumes
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IAceReader OpenReader(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeAceReader(streams, options ?? new ReaderOptions());
    }

    public static ValueTask<IAceAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IAceAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IAceAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAceAsyncReader)OpenReader(stream, readerOptions));
    }

    public static IAceAsyncReader OpenAsyncReader(
        IEnumerable<Stream> streams,
        ReaderOptions? options = null
    )
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeAceReader(streams, options ?? new ReaderOptions());
    }

    public static ValueTask<IAceAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAceAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IAceReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IAceReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
