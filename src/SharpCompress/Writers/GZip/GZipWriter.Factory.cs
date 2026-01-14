#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter : IWriterOpenable<GZipWriterOptions>
{
    public static IWriter Open(string filePath, GZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), writerOptions);
    }

    public static IWriter Open(FileInfo fileInfo, GZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new GZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter Open(Stream stream, GZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new GZipWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        string path,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(path, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        Stream stream,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        FileInfo fileInfo,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(fileInfo, writerOptions);
    }
}
#endif
