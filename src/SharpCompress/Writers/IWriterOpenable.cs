#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;

namespace SharpCompress.Writers;

public interface IWriterOpenable<TWriterOptions>
    where TWriterOptions : IWriterOptions
{
    public static abstract IWriter OpenWriter(string filePath, TWriterOptions writerOptions);

    public static abstract IWriter OpenWriter(FileInfo fileInfo, TWriterOptions writerOptions);
    public static abstract IWriter OpenWriter(Stream stream, TWriterOptions writerOptions);

    /// <summary>
    /// Opens a Writer asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="writerOptions">Writer options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that returns an async writer.</returns>
    public static abstract ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );

    public static abstract ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );
}
#endif
