#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter : IWriterOpenable<TarWriterOptions>
{
    public static IWriter OpenWriter(string filePath, TarWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IWriter OpenWriter(FileInfo fileInfo, TarWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new TarWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter OpenWriter(Stream stream, TarWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new TarWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        string path,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(path, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        Stream stream,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
