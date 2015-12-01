namespace SharpCompress.Common.Zip.Headers
{
    using SharpCompress.Common.Zip;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal class LocalEntryHeader : ZipFileEntry
    {
        [CompilerGenerated]
        private ushort _Version_k__BackingField;

        public LocalEntryHeader() : base(ZipHeaderType.LocalEntry)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            this.Version = reader.ReadUInt16();
            base.Flags = (HeaderFlags) reader.ReadUInt16();
            base.CompressionMethod = (ZipCompressionMethod) reader.ReadUInt16();
            base.LastModifiedTime = reader.ReadUInt16();
            base.LastModifiedDate = reader.ReadUInt16();
            base.Crc = reader.ReadUInt32();
            base.CompressedSize = reader.ReadUInt32();
            base.UncompressedSize = reader.ReadUInt32();
            ushort count = reader.ReadUInt16();
            ushort num2 = reader.ReadUInt16();
            byte[] str = reader.ReadBytes(count);
            byte[] extra = reader.ReadBytes(num2);
            base.Name = base.DecodeString(str);
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
            writer.Write(buffer);
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
    }
}

