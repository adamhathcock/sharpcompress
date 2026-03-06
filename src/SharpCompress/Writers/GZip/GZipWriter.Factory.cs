#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter : IWriterOpenable<GZipWriterOptions>
{
    public static IWriter OpenWriter(string filePath, GZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IWriter OpenWriter(FileInfo fileInfo, GZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new GZipWriter(fileInfo.OpenWrite(), writerOptions with { LeaveStreamOpen = false });
    }

    public static IWriter OpenWriter(Stream stream, GZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new GZipWriter(stream, writerOptions);
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(filePath, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(stream, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        GZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(fileInfo, writerOptions));
    }
}
#endif
