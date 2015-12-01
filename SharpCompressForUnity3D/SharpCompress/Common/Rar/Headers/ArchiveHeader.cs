namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class ArchiveHeader : RarHeader
    {
        [CompilerGenerated]
        private byte _EncryptionVersion_k__BackingField;
        [CompilerGenerated]
        private short _HighPosAv_k__BackingField;
        [CompilerGenerated]
        private int _PosAv_k__BackingField;
        internal const short mainHeaderSize = 6;
        internal const short mainHeaderSizeWithEnc = 7;

        private bool ArchiveHeaderFlags_HasFlag(ArchiveFlags archiveFlags)
        {
            return ((this.ArchiveHeaderFlags & archiveFlags) == archiveFlags);
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            this.HighPosAv = reader.ReadInt16();
            this.PosAv = reader.ReadInt32();
            if (this.ArchiveHeaderFlags_HasFlag(ArchiveFlags.ENCRYPTVER))
            {
                this.EncryptionVersion = reader.ReadByte();
            }
        }

        internal ArchiveFlags ArchiveHeaderFlags
        {
            get
            {
                return (ArchiveFlags) base.Flags;
            }
        }

        internal byte EncryptionVersion
        {
            [CompilerGenerated]
            get
            {
                return this._EncryptionVersion_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._EncryptionVersion_k__BackingField = value;
            }
        }

        public bool HasPassword
        {
            get
            {
                return this.ArchiveHeaderFlags_HasFlag(ArchiveFlags.PASSWORD);
            }
        }

        internal short HighPosAv
        {
            [CompilerGenerated]
            get
            {
                return this._HighPosAv_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._HighPosAv_k__BackingField = value;
            }
        }

        internal int PosAv
        {
            [CompilerGenerated]
            get
            {
                return this._PosAv_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._PosAv_k__BackingField = value;
            }
        }
    }
}

