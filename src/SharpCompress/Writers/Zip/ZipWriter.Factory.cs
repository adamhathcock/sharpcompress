#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter : IWriterOpenable<ZipWriterOptions>
{
    public static IWriter OpenWriter(string filePath, ZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IWriter OpenWriter(FileInfo fileInfo, ZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new ZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter OpenWriter(Stream stream, ZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new ZipWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        string path,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(path, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        Stream stream,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
