using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class ArchiveHeader : RarHeader
    {
        public ArchiveHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader, HeaderType.Archive) 
        {
        }

        protected override void ReadFinish(MarkingBinaryReader reader)
        {
            if (IsRar5) 
            {
                Flags = reader.ReadRarVIntUInt16();
                if (HasFlag(ArchiveFlagsV5.HasVolumeNumber))
                {
                    VolumeNumber = (int)reader.ReadRarVIntUInt32();
                }
                if (ExtraSize != 0) {
                    ReadLocator(reader);
                }
            } 
            else 
            {
                Flags = HeaderFlags;
                HighPosAv = reader.ReadInt16();
                PosAv = reader.ReadInt32();
                if (HasFlag(ArchiveFlagsV4.EncryptVer))
                {
                    EncryptionVersion = reader.ReadByte();
                }
            }
        }

        private void ReadLocator(MarkingBinaryReader reader) {
            // parse fields, unused
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

        private ushort Flags  { get; set; }

        private bool HasFlag(ushort flag) 
        {
            return (Flags & flag) == flag;
        }

        internal int? VolumeNumber { get; private set; }

        internal short? HighPosAv { get; private set; }

        internal int? PosAv { get; private set; }

        private byte? EncryptionVersion { get; set; }

        public bool? IsEncrypted => IsRar5 ? (bool?)null : HasFlag(ArchiveFlagsV4.Password);

        public bool OldNumberingFormat => !IsRar5 && !HasFlag(ArchiveFlagsV4.NewNumbering);

        public bool IsVolume => HasFlag(IsRar5 ? ArchiveFlagsV5.Volume : ArchiveFlagsV4.Volume);

        public bool IsFirstVolume => IsRar5 ? VolumeNumber == 1 : HasFlag(ArchiveFlagsV4.FirstVolume);

        public bool IsSolid => HasFlag(IsRar5 ? ArchiveFlagsV5.Solid : ArchiveFlagsV4.Solid);
    }
}