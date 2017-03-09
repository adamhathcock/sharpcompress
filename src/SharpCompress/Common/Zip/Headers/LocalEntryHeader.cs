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
            if (IsZip64)
                Version = (ushort)(Version > 45 ? Version : 45);

            writer.Write(Version);
            
            writer.Write((ushort)Flags);
            writer.Write((ushort)CompressionMethod);
            writer.Write(LastModifiedTime);
            writer.Write(LastModifiedDate);
            writer.Write(Crc);

            if (IsZip64)
            {
                writer.Write(uint.MaxValue);
                writer.Write(uint.MaxValue);
            }
            else
            {
                writer.Write(CompressedSize);
                writer.Write(UncompressedSize);
            }

            byte[] nameBytes = EncodeString(Name);

            writer.Write((ushort)nameBytes.Length);
            if (IsZip64)
            {
                writer.Write((ushort)(2 + 2 + (2 * 8)));
            }
            else
            {
                writer.Write((ushort)0);
            }

            //if (Extra != null)
            //{
            //    writer.Write(Extra);
            //}
            writer.Write(nameBytes);
            if (IsZip64)
            {
                writer.Write((ushort)0x0001);
                writer.Write((ushort)(2 * 8));
                writer.Write((ulong)CompressedSize);
                writer.Write((ulong)UncompressedSize);
            }
        }

        internal ushort Version { get; private set; }
    }
}