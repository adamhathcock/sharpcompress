#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter : IWriterOpenable<ZipWriterOptions>
{
    public static IWriter Open(string filePath, ZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), writerOptions);
    }

    public static IWriter Open(FileInfo fileInfo, ZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new ZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter Open(Stream stream, ZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new ZipWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        string path,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(path, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        Stream stream,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        FileInfo fileInfo,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(fileInfo, writerOptions);
    }
}
#endif
