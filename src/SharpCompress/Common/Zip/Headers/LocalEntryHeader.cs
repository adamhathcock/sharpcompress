using System.IO;
using System.Linq;

namespace SharpCompress.Common.Zip.Headers
{
    internal class LocalEntryHeader : ZipFileEntry
    {
        public LocalEntryHeader()
            : base(ZipHeaderType.LocalEntry)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            Version = reader.ReadUInt16();
            Flags = (HeaderFlags)reader.ReadUInt16();
            CompressionMethod = (ZipCompressionMethod)reader.ReadUInt16();
            LastModifiedTime = reader.ReadUInt16();
            LastModifiedDate = reader.ReadUInt16();
            Crc = reader.ReadUInt32();
            CompressedSize = reader.ReadUInt32();
            UncompressedSize = reader.ReadUInt32();
            ushort nameLength = reader.ReadUInt16();
            ushort extraLength = reader.ReadUInt16();
            byte[] name = reader.ReadBytes(nameLength);
            byte[] extra = reader.ReadBytes(extraLength);
            Name = DecodeString(name);
            LoadExtra(extra);

            var unicodePathExtra = Extra.FirstOrDefault(u => u.Type == ExtraDataType.UnicodePathExtraField);
            if (unicodePathExtra != null)
            {
                Name = ((ExtraUnicodePathExtraField)unicodePathExtra).UnicodeName;
            }

            var zip64ExtraData = Extra.OfType<Zip64ExtendedInformationExtraField>().FirstOrDefault();
            if (zip64ExtraData != null)
            {
                if (CompressedSize == uint.MaxValue)
                {
                    CompressedSize = zip64ExtraData.CompressedSize;
                }
                if (UncompressedSize == uint.MaxValue)
                {
                    UncompressedSize = zip64ExtraData.UncompressedSize;
                }
            }
        }

        internal override void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write((ushort)Flags);
            writer.Write((ushort)CompressionMethod);
            writer.Write(LastModifiedTime);
            writer.Write(LastModifiedDate);
            writer.Write(Crc);
			writer.Write((uint)CompressedSize);
			writer.Write((uint)UncompressedSize);

            byte[] nameBytes = EncodeString(Name);

            writer.Write((ushort)nameBytes.Length);
            writer.Write((ushort)0);

            //if (Extra != null)
            //{
            //    writer.Write(Extra);
            //}
            writer.Write(nameBytes);
        }

        internal ushort Version { get; private set; }
    }
}