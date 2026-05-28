using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class EndArchiveHeader : RarHeader
{
    public static EndArchiveHeader Create(RarHeader header, RarCrcBinaryReader reader) =>
        CreateChild<EndArchiveHeader>(header, reader, HeaderType.EndArchive);

    protected sealed override void ReadFinish(MarkingBinaryReader reader)
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

    private ushort Flags { get; set; }

    private bool HasFlag(ushort flag) => (Flags & flag) == flag;

    internal int? ArchiveCrc { get; private set; }

    internal short? VolumeNumber { get; private set; }
}
