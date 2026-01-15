#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;

namespace SharpCompress.Writers;

public interface IWriterOpenable<TWriterOptions>
    where TWriterOptions : WriterOptions
{
    public static abstract IWriter OpenWriter(string filePath, TWriterOptions writerOptions);

    public static abstract IWriter OpenWriter(FileInfo fileInfo, TWriterOptions writerOptions);
    public static abstract IWriter OpenWriter(Stream stream, TWriterOptions writerOptions);

    /// <summary>
    /// Opens a Writer asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="archiveType">The archive type.</param>
    /// <param name="writerOptions">Writer options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that returns an IWriter.</returns>
    public static abstract IAsyncWriter OpenAsyncWriter(
        Stream stream,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );

    public static abstract IAsyncWriter OpenAsyncWriter(
        string filePath,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );

    public static abstract IAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        TWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );
}
#endif
