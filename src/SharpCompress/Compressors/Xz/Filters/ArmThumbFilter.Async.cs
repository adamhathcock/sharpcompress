using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz.Filters;

public partial class ArmThumbFilter
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => await BaseStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#endif
}
