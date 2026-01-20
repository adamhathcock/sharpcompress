using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class EndArchiveHeader : RarHeader
{
    public static EndArchiveHeader Create(RarHeader header, RarCrcBinaryReader reader) =>
        CreateChild<EndArchiveHeader>(header, reader, HeaderType.EndArchive);

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

    protected override void ReadFinish(MarkingBinaryReader reader)
    {
        if (IsRar5)
        {
            Flags = reader.ReadRarVIntUInt16();
        }
        else
        {
            Flags = HeaderFlags;
            if (HasFlag(EndArchiveFlagsV4.DATA_CRC))
            {
                ArchiveCrc = reader.ReadInt32();
            }
            if (HasFlag(EndArchiveFlagsV4.VOLUME_NUMBER))
            {
                VolumeNumber = reader.ReadInt16();
            }
        }
    }

    protected override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        if (IsRar5)
        {
            Flags = await reader.ReadRarVIntUInt16Async(cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private ushort Flags { get; set; }

    private bool HasFlag(ushort flag) => (Flags & flag) == flag;

    internal int? ArchiveCrc { get; private set; }

    internal short? VolumeNumber { get; private set; }
}
