using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class ArchiveHeader : RarHeader
    {
        internal const short mainHeaderSizeWithEnc = 7;
        internal const short mainHeaderSize = 6;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            HighPosAv = reader.ReadInt16();
            PosAv = reader.ReadInt32();
            if (ArchiveHeaderFlags.HasFlag(ArchiveFlags.ENCRYPTVER))
            {
                EncryptionVersion = reader.ReadByte();
            }
        }

        internal ArchiveFlags ArchiveHeaderFlags
        {
            get
            {
                return (ArchiveFlags)base.Flags;
            }
        }

        internal short HighPosAv
        {
            get;
            private set;
        }

        internal int PosAv
        {
            get;
            private set;
        }

        internal byte EncryptionVersion
        {
            get;
            private set;
        }
    }
}
