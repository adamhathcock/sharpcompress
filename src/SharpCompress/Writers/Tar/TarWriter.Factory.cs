#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter : IWriterOpenable<TarWriterOptions>
{
    public static IWriter Open(string filePath, TarWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), writerOptions);
    }

    public static IWriter Open(FileInfo fileInfo, TarWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new TarWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter Open(Stream stream, TarWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new TarWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        string path,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(path, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        Stream stream,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        FileInfo fileInfo,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(fileInfo, writerOptions);
    }
}
#endif
