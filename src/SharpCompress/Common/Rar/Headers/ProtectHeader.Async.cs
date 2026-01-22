using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class ProtectHeader
{
    public static async ValueTask<ProtectHeader> CreateAsync(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        var c = await CreateChildAsync<ProtectHeader>(
                header,
                reader,
                HeaderType.Protect,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (c.IsRar5)
        {
            throw new InvalidFormatException("unexpected rar5 record");
        }
        return c;
    }

    protected sealed override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        Version = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        RecSectors = await reader.ReadUInt16Async(cancellationToken).ConfigureAwait(false);
        TotalBlocks = await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false);
        Mark = await reader.ReadBytesAsync(8, cancellationToken).ConfigureAwait(false);
    }
}
