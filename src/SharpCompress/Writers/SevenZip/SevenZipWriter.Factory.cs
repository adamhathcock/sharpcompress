#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Writers.SevenZip;

public partial class SevenZipWriter : IWriterOpenable<SevenZipWriterOptions>
{
    /// <summary>
    /// Opens a new SevenZipWriter for the specified file path.
    /// </summary>
    public static IWriter OpenWriter(string filePath, SevenZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    /// <summary>
    /// Opens a new SevenZipWriter for the specified file.
    /// </summary>
    public static IWriter OpenWriter(FileInfo fileInfo, SevenZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new SevenZipWriter(
            fileInfo.OpenWrite(),
            writerOptions with
            {
                LeaveStreamOpen = false,
            }
        );
    }

    /// <summary>
    /// Opens a new SevenZipWriter for the specified stream.
    /// </summary>
    public static IWriter OpenWriter(Stream stream, SevenZipWriterOptions writerOptions)
    {
        stream.RequireWritable();
        return new SevenZipWriter(stream, writerOptions);
    }

    /// <summary>
    /// Opens a new async SevenZipWriter for the specified file path.
    /// </summary>
    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        SevenZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(filePath, writerOptions));
    }

    /// <summary>
    /// Opens a new async SevenZipWriter for the specified stream.
    /// </summary>
    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        SevenZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(stream, writerOptions));
    }

    /// <summary>
    /// Opens a new async SevenZipWriter for the specified file.
    /// </summary>
    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        SevenZipWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncWriter)OpenWriter(fileInfo, writerOptions));
    }
}
#endif
