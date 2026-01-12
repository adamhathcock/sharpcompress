using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public static class IWritableAsyncArchiveExtensions
{
    public static ValueTask SaveToAsync(
        this IWritableAsyncArchive writableArchive,
        string filePath,
        WriterOptions? options = null,
        CancellationToken cancellationToken = default
    ) => writableArchive.SaveToAsync(new FileInfo(filePath), options ?? new (CompressionType.Deflate), cancellationToken);

    public static async ValueTask SaveToAsync(
        this IWritableAsyncArchive writableArchive,
        FileInfo fileInfo,
        WriterOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        await writableArchive.SaveToAsync(stream, options?? new (CompressionType.Deflate),  cancellationToken).ConfigureAwait(false);
    }

}
