using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip;

internal partial class SeekableZipFilePart
{
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_isLocalHeaderLoaded)
        {
            await LoadLocalHeaderAsync(cancellationToken).ConfigureAwait(false);
            _isLocalHeaderLoaded = true;
        }
        return await base.GetCompressedStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask LoadLocalHeaderAsync(CancellationToken cancellationToken = default) =>
        Header = await _headerFactory
            .GetLocalHeaderAsync(BaseStream, (DirectoryEntryHeader)Header)
            .ConfigureAwait(false);
}
