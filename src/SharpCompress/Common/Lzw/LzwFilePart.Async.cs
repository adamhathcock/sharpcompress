using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Lzw;

internal sealed partial class LzwFilePart
{
    internal static async ValueTask<LzwFilePart> CreateAsync(
        Stream stream,
        IArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var part = new LzwFilePart(stream, archiveEncoding);

        // For non-seekable streams, we can't track position, so use 0 since the stream will be
        // read sequentially from its current position.
        part.EntryStartPosition = stream.CanSeek ? stream.Position : 0;
        return part;
    }
}
