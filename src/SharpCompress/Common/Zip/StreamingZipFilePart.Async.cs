using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed partial class StreamingZipFilePart
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        var decompressionStream = await CreateDecompressionStreamAsync(
                await GetCryptoStreamAsync(CreateBaseStream(), cancellationToken)
                    .ConfigureAwait(false),
                Header.CompressionMethod,
                cancellationToken
            )
            .ConfigureAwait(false);
        _decompressionStream = decompressionStream;
        if (LeaveStreamOpen)
        {
            return SharpCompressStream.CreateNonDisposing(decompressionStream);
        }
        return decompressionStream;
    }
}
