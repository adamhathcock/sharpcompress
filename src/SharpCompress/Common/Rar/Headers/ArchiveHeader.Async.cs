using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class ArchiveHeader
{
    public static async ValueTask<ArchiveHeader> CreateAsync(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        CancellationToken cancellationToken = default
    ) =>
        await CreateChildAsync<ArchiveHeader>(header, reader, HeaderType.Archive, cancellationToken)
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
            if (HasFlag(ArchiveFlagsV5.HAS_VOLUME_NUMBER))
            {
                VolumeNumber = (int)
                    await reader
                        .ReadRarVIntUInt32Async(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
            }
            // later: we may have a locator record if we need it
            //if (ExtraSize != 0) {
            //    ReadLocator(reader);
            //}
        }
        else
        {
            Flags = HeaderFlags;
            HighPosAv = await reader.ReadInt16Async(cancellationToken).ConfigureAwait(false);
            PosAv = await reader.ReadInt32Async(cancellationToken).ConfigureAwait(false);
            if (HasFlag(ArchiveFlagsV4.ENCRYPT_VER))
            {
                _ = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
