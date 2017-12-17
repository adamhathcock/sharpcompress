using System;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class ArchiveHeader : RarHeader
    {
        internal const short mainHeaderSizeWithEnc = 7;
        internal const short mainHeaderSize = 6;

        public ArchiveHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader) {
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (this.isRar5) 
            {
                ArchiveHeaderFlags = reader.ReadRarVIntUInt16();
                if ((ArchiveHeaderFlags & ArchiveFlagsV5.HasVolumeNumber) == ArchiveFlagsV5.HasVolumeNumber)
                {
                    VolumeNumber = (int)reader.ReadRarVIntUInt32();
                }
                if (ExtraSize != 0) {
                    ReadLocator(reader);
                }
            } else {
                ArchiveHeaderFlags = Flags;
                HighPosAv = reader.ReadInt16();
                PosAv = reader.ReadInt32();
                if ((ArchiveHeaderFlags & ArchiveFlagsV4.EncryptVer) == ArchiveFlagsV4.EncryptVer)
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
            const ushort HasQuickOpenOffset = 0x01;
            const ushort HasRecoveryOffset = 0x02;
            ulong quickOpenOffset = 0;
            if ((flags & HasQuickOpenOffset) == HasQuickOpenOffset) { 
                quickOpenOffset = reader.ReadRarVInt();
            }
            ulong recoveryOffset = 0;
            if ((flags & HasRecoveryOffset) == HasRecoveryOffset) { 
                recoveryOffset = reader.ReadRarVInt();
            }
        }

        internal ushort ArchiveHeaderFlags  { get; private set; }

        internal int VolumeNumber { get; private set; }

        internal short HighPosAv { get; private set; }

        internal int PosAv { get; private set; }

        private byte EncryptionVersion { get; set; }

        public bool HasPassword => (ArchiveHeaderFlags & ArchiveFlagsV4.Password) == ArchiveFlagsV4.Password;

        public bool OldNumberingFormat { 
            get {
                return !this.isRar5 && 
                    ((ArchiveHeaderFlags & ArchiveFlagsV4.NewNumbering) != ArchiveFlagsV4.NewNumbering);
            }
        }

        public bool IsVolume { 
            get { 
                return this.isRar5
                    ? ((ArchiveHeaderFlags & ArchiveFlagsV5.Volume) == ArchiveFlagsV5.Volume)
                    : ((ArchiveHeaderFlags & ArchiveFlagsV4.Volume) == ArchiveFlagsV4.Volume);
                } 
        }

        public bool IsFirstVolume {
            get {
                return this.isRar5
                    ? VolumeNumber == 1
                    : ((ArchiveHeaderFlags & ArchiveFlagsV4.FirstVolume) == ArchiveFlagsV4.FirstVolume);
            }
        }

        public bool IsSolid { 
            get {
                return this.isRar5
                    ? ((ArchiveHeaderFlags & ArchiveFlagsV5.Solid) == ArchiveFlagsV5.Solid)
                    : ((ArchiveHeaderFlags & ArchiveFlagsV4.Solid) == ArchiveFlagsV4.Solid);
            }
        }
    }
}