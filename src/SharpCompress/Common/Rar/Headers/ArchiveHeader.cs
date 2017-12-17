using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class ArchiveHeader : RarHeader
    {
        public ArchiveHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader, HeaderType.Archive) {
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (this.IsRar5) 
            {
                ArchiveHeaderFlags = reader.ReadRarVIntUInt16();
                if (HasArchiveHeaderFlag(ArchiveFlagsV5.HasVolumeNumber))
                {
                    VolumeNumber = (int)reader.ReadRarVIntUInt32();
                }
                if (ExtraSize != 0) {
                    ReadLocator(reader);
                }
            } 
            else 
            {
                ArchiveHeaderFlags = HeaderFlags;
                HighPosAv = reader.ReadInt16();
                PosAv = reader.ReadInt32();
                if (HasArchiveHeaderFlag(ArchiveFlagsV4.EncryptVer))
                {
                    EncryptionVersion = reader.ReadByte();
                }
            }
        }

        private void ReadLocator(MarkingBinaryReader reader) {
            var size = reader.ReadRarVIntUInt16();
            var type = reader.ReadRarVIntUInt16();
            if (type != 1) throw new InvalidFormatException("expected locator record");
            var flags = reader.ReadRarVIntUInt16();
            const ushort hasQuickOpenOffset = 0x01;
            const ushort hasRecoveryOffset = 0x02;
            ulong quickOpenOffset = 0;
            if ((flags & hasQuickOpenOffset) == hasQuickOpenOffset) { 
                quickOpenOffset = reader.ReadRarVInt();
            }
            ulong recoveryOffset = 0;
            if ((flags & hasRecoveryOffset) == hasRecoveryOffset) { 
                recoveryOffset = reader.ReadRarVInt();
            }
        }

        private ushort ArchiveHeaderFlags  { get; set; }

        private bool HasArchiveHeaderFlag(ushort flag) 
        {
            return (ArchiveHeaderFlags & flag) == flag;
        }

        internal int? VolumeNumber { get; private set; }

        internal short? HighPosAv { get; private set; }

        internal int? PosAv { get; private set; }

        private byte? EncryptionVersion { get; set; }

        public bool? IsEncrypted => this.IsRar5 ? (bool?)null : HasArchiveHeaderFlag(ArchiveFlagsV4.Password);

        public bool OldNumberingFormat => !this.IsRar5 && !HasArchiveHeaderFlag(ArchiveFlagsV4.NewNumbering);

        public bool IsVolume => HasArchiveHeaderFlag(this.IsRar5 ? ArchiveFlagsV5.Volume : ArchiveFlagsV4.Volume);

        public bool IsFirstVolume => this.IsRar5 ? VolumeNumber == 1 : HasArchiveHeaderFlag(ArchiveFlagsV4.FirstVolume);

        public bool IsSolid => HasArchiveHeaderFlag(this.IsRar5 ? ArchiveFlagsV5.Solid : ArchiveFlagsV4.Solid);
    }
}