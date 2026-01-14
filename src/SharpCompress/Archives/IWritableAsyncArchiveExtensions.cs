using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public static class IWritableAsyncArchiveExtensions
{
    extension(IWritableAsyncArchive writableArchive)
    {
        public ValueTask SaveToAsync(
            string filePath,
            WriterOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            writableArchive.SaveToAsync(
                new FileInfo(filePath),
                options ?? new(CompressionType.Deflate),
                cancellationToken
            );

        public async ValueTask SaveToAsync(
            FileInfo fileInfo,
            WriterOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
            await writableArchive
                .SaveToAsync(stream, options ?? new(CompressionType.Deflate), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
