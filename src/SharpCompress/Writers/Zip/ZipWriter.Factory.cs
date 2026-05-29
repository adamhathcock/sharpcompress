#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        return new ZipWriter(fileInfo.OpenWrite(), writerOptions with { LeaveStreamOpen = false });
    }

    public static IWriter OpenWriter(Stream stream, ZipWriterOptions writerOptions)
    {
        stream.RequireWritable();
        return new ZipWriter(stream, writerOptions);
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(filePath, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(stream, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        ZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(fileInfo, writerOptions));
    }
}
#endif
