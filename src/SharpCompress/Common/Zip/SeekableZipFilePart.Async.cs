using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip;

internal partial class SeekableZipFilePart
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
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

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask LoadLocalHeaderAsync(CancellationToken cancellationToken = default) =>
        Header = await _headerFactory
            .GetLocalHeaderAsync(BaseStream, _directoryEntryHeader)
            .ConfigureAwait(false);

    internal ValueTask<LocalEntryHeader> GetRawLocalHeaderAsync() =>
        _headerFactory.GetRawLocalHeaderAsync(BaseStream, _directoryEntryHeader);
}
