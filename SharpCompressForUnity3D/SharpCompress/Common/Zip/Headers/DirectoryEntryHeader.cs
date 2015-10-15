namespace SharpCompress.Common.Zip.Headers
{
    using SharpCompress.Common.Zip;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal class DirectoryEntryHeader : ZipFileEntry
    {
        [CompilerGenerated]
        private string _Comment_k__BackingField;
        [CompilerGenerated]
        private ushort _DiskNumberStart_k__BackingField;
        [CompilerGenerated]
        private uint _ExternalFileAttributes_k__BackingField;
        [CompilerGenerated]
        private ushort _InternalFileAttributes_k__BackingField;
        [CompilerGenerated]
        private uint _RelativeOffsetOfEntryHeader_k__BackingField;
        [CompilerGenerated]
        private ushort _Version_k__BackingField;
        [CompilerGenerated]
        private ushort _VersionNeededToExtract_k__BackingField;

        public DirectoryEntryHeader() : base(ZipHeaderType.DirectoryEntry)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            this.Version = reader.ReadUInt16();
            this.VersionNeededToExtract = reader.ReadUInt16();
            base.Flags = (HeaderFlags) reader.ReadUInt16();
            base.CompressionMethod = (ZipCompressionMethod) reader.ReadUInt16();
            base.LastModifiedTime = reader.ReadUInt16();
            base.LastModifiedDate = reader.ReadUInt16();
            base.Crc = reader.ReadUInt32();
            base.CompressedSize = reader.ReadUInt32();
            base.UncompressedSize = reader.ReadUInt32();
            ushort count = reader.ReadUInt16();
            ushort num2 = reader.ReadUInt16();
            ushort num3 = reader.ReadUInt16();
            this.DiskNumberStart = reader.ReadUInt16();
            this.InternalFileAttributes = reader.ReadUInt16();
            this.ExternalFileAttributes = reader.ReadUInt32();
            this.RelativeOffsetOfEntryHeader = reader.ReadUInt32();
            byte[] str = reader.ReadBytes(count);
            base.Name = base.DecodeString(str);
            byte[] extra = reader.ReadBytes(num2);
            byte[] buffer3 = reader.ReadBytes(num3);
            this.Comment = base.DecodeString(buffer3);
            base.LoadExtra(extra);
            ExtraData data = Enumerable.FirstOrDefault<ExtraData>(base.Extra, delegate (ExtraData u) {
                return u.Type == ExtraDataType.UnicodePathExtraField;
            });
            if (data != null)
            {
                base.Name = ((ExtraUnicodePathExtraField) data).UnicodeName;
            }
        }

        internal override void Write(BinaryWriter writer)
        {
            writer.Write(this.Version);
            writer.Write(this.VersionNeededToExtract);
            writer.Write((ushort) base.Flags);
            writer.Write((ushort) base.CompressionMethod);
            writer.Write(base.LastModifiedTime);
            writer.Write(base.LastModifiedDate);
            writer.Write(base.Crc);
            writer.Write(base.CompressedSize);
            writer.Write(base.UncompressedSize);
            byte[] buffer = base.EncodeString(base.Name);
            writer.Write((ushort) buffer.Length);
            writer.Write((ushort) 0);
            writer.Write((ushort) this.Comment.Length);
            writer.Write(this.DiskNumberStart);
            writer.Write(this.InternalFileAttributes);
            writer.Write(this.ExternalFileAttributes);
            writer.Write(this.RelativeOffsetOfEntryHeader);
            writer.Write(buffer);
            writer.Write(this.Comment);
        }

        public string Comment
        {
            [CompilerGenerated]
            get
            {
                return this._Comment_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Comment_k__BackingField = value;
            }
        }

        public ushort DiskNumberStart
        {
            [CompilerGenerated]
            get
            {
                return this._DiskNumberStart_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._DiskNumberStart_k__BackingField = value;
            }
        }

        public uint ExternalFileAttributes
        {
            [CompilerGenerated]
            get
            {
                return this._ExternalFileAttributes_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._ExternalFileAttributes_k__BackingField = value;
            }
        }

        public ushort InternalFileAttributes
        {
            [CompilerGenerated]
            get
            {
                return this._InternalFileAttributes_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._InternalFileAttributes_k__BackingField = value;
            }
        }

        public uint RelativeOffsetOfEntryHeader
        {
            [CompilerGenerated]
            get
            {
                return this._RelativeOffsetOfEntryHeader_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._RelativeOffsetOfEntryHeader_k__BackingField = value;
            }
        }

        internal ushort Version
        {
            [CompilerGenerated]
            get
            {
                return this._Version_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Version_k__BackingField = value;
            }
        }

        public ushort VersionNeededToExtract
        {
            [CompilerGenerated]
            get
            {
                return this._VersionNeededToExtract_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._VersionNeededToExtract_k__BackingField = value;
            }
        }
    }
}

