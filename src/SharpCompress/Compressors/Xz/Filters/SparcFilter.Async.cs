using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz.Filters;

public partial class SparcFilter
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadBaseAsync(buffer, offset, count, cancellationToken);

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#endif
}
