using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed partial class StreamingZipFilePart
{
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        _decompressionStream = await CreateDecompressionStreamAsync(
                await GetCryptoStreamAsync(CreateBaseStream(), cancellationToken)
                    .ConfigureAwait(false),
                Header.CompressionMethod,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (LeaveStreamOpen)
        {
            return SharpCompressStream.Create(_decompressionStream, leaveOpen: true);
        }
        return _decompressionStream;
    }
}
