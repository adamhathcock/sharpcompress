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

        if (stream.CanSeek)
        {
            part.EntryStartPosition = stream.Position;
        }
        else
        {
            // For non-seekable streams, we can't track position.
            // Set to 0 since the stream will be read sequentially from its current position.
            part.EntryStartPosition = 0;
        }
        return part;
    }
}
