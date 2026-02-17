#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common.Options;

namespace SharpCompress.Writers;

public interface IWriterOpenable<TWriter, TAsyncWriter, TWriterOptions>
    where TWriter : IWriter
    where TAsyncWriter : IAsyncWriter
    where TWriterOptions : IWriterOptions
{
    public static abstract TWriter OpenWriter(string filePath, TWriterOptions writerOptions);

    public static abstract TWriter OpenWriter(FileInfo fileInfo, TWriterOptions writerOptions);
    public static abstract TWriter OpenWriter(Stream stream, TWriterOptions writerOptions);

    /// <summary>
    /// Opens a Writer asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="archiveType">The archive type.</param>
    /// <param name="writerOptions">Writer options.</param>
    /// <returns>A task that returns an IWriter.</returns>
    public static abstract TAsyncWriter OpenAsyncWriter(
        Stream stream,
        TWriterOptions writerOptions
    );

    public static abstract TAsyncWriter OpenAsyncWriter(
        string filePath,
        TWriterOptions writerOptions
    );

    public static abstract TAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        TWriterOptions writerOptions
    );
}
#endif
