using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class EndArchiveHeader
{
    public static async ValueTask<EndArchiveHeader> CreateAsync(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        CancellationToken cancellationToken = default
    ) =>
        await CreateChildAsync<EndArchiveHeader>(
                header,
                reader,
                HeaderType.EndArchive,
                cancellationToken
            )
            .ConfigureAwait(false);

    protected sealed override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        if (IsRar5)
        {
            Flags = await reader
                .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            Flags = HeaderFlags;
            if (HasFlag(EndArchiveFlagsV4.DATA_CRC))
            {
                ArchiveCrc = await reader.ReadInt32Async(cancellationToken).ConfigureAwait(false);
            }
            if (HasFlag(EndArchiveFlagsV4.VOLUME_NUMBER))
            {
                VolumeNumber = await reader.ReadInt16Async(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
