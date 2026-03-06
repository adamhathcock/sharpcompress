#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        return new TarWriter(fileInfo.OpenWrite(), writerOptions with { LeaveStreamOpen = false });
    }

    public static IWriter OpenWriter(Stream stream, TarWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new TarWriter(stream, writerOptions);
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(filePath, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(stream, writerOptions));
    }

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        TarWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(fileInfo, writerOptions));
    }
}
#endif
