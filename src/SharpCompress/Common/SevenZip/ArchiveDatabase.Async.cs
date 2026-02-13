using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Common.SevenZip;

internal sealed partial class ArchiveDatabase
{
    internal async ValueTask<Stream> GetFolderStreamAsync(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        CancellationToken cancellationToken
    )
    {
        var packStreamIndex = folder._firstPackStreamId;
        var folderStartPackPos = GetFolderStreamPos(folder, 0);
        var count = folder._packStreams.Count;
        var packSizes = new long[count];
        for (var j = 0; j < count; j++)
        {
            packSizes[j] = _packSizes[packStreamIndex + j];
        }

        return await DecoderStreamHelper
            .CreateDecoderStreamAsync(
                stream,
                folderStartPackPos,
                packSizes,
                folder,
                pw,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
