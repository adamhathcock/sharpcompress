using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.Ace;

public partial class AceReader
#if NET8_0_OR_GREATER
    : IReaderOpenable
#endif
{
    /// <summary>
    /// Opens an AceReader for non-seeking usage with a single volume.
    /// </summary>
    /// <param name="stream">The stream containing the ACE archive.</param>
    /// <param name="readerOptions">Reader options.</param>
    /// <returns>An AceReader instance.</returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
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
    public static IReader OpenReader(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeAceReader(streams, options ?? new ReaderOptions());
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        path.NotNullOrEmpty(nameof(path));
        return new((IAsyncReader)OpenReader(new FileInfo(path), readerOptions));
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)OpenReader(stream, readerOptions));
    }

    public static IAsyncReader OpenAsyncReader(
        IEnumerable<Stream> streams,
        ReaderOptions? options = null
    )
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeAceReader(streams, options ?? new ReaderOptions());
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)OpenReader(fileInfo, readerOptions));
    }

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
