using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.Common.Zip.Headers
{
    internal class LocalEntryHeader : ZipFileEntry
    {
        public LocalEntryHeader(ArchiveEncoding archiveEncoding)
            : base(ZipHeaderType.LocalEntry, archiveEncoding)
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
            Name = ArchiveEncoding.Decode(name);
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

        internal ushort Version { get; private set; }
    }
}