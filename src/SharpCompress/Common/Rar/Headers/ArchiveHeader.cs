using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed class ArchiveHeader : RarHeader
{
    public ArchiveHeader(RarHeader header, RarCrcBinaryReader reader)
        : base(header, reader, HeaderType.Archive) { }

    protected override void ReadFinish(MarkingBinaryReader reader)
    {
        if (IsRar5)
        {
            Flags = reader.ReadRarVIntUInt16();
            if (HasFlag(ArchiveFlagsV5.HAS_VOLUME_NUMBER))
            {
                VolumeNumber = (int)reader.ReadRarVIntUInt32();
            }
            // later: we may have a locator record if we need it
            //if (ExtraSize != 0) {
            //    ReadLocator(reader);
            //}
        }
        else
        {
            Flags = HeaderFlags;
            HighPosAv = reader.ReadInt16();
            PosAv = reader.ReadInt32();
            if (HasFlag(ArchiveFlagsV4.ENCRYPT_VER))
            {
                EncryptionVersion = reader.ReadByte();
            }
        }
    }

    private ushort Flags { get; set; }

    private bool HasFlag(ushort flag) => (Flags & flag) == flag;

    internal int? VolumeNumber { get; private set; }

    internal short? HighPosAv { get; private set; }

    internal int? PosAv { get; private set; }

    private byte? EncryptionVersion { get; set; }

    public bool? IsEncrypted => IsRar5 ? null : HasFlag(ArchiveFlagsV4.PASSWORD);

    public bool OldNumberingFormat => !IsRar5 && !HasFlag(ArchiveFlagsV4.NEW_NUMBERING);

    public bool IsVolume => HasFlag(IsRar5 ? ArchiveFlagsV5.VOLUME : ArchiveFlagsV4.VOLUME);

    // RAR5: Volume number field is present. True for all volumes except first.
    public bool IsFirstVolume =>
        IsRar5 ? VolumeNumber is null : HasFlag(ArchiveFlagsV4.FIRST_VOLUME);

    public bool IsSolid => HasFlag(IsRar5 ? ArchiveFlagsV5.SOLID : ArchiveFlagsV4.SOLID);
}
