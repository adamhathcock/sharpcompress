using System;
using System.IO;
using System.Linq;

namespace SharpCompress.Common.Zip.Headers
{
    internal class DirectoryEntryHeader : ZipFileEntry
    {
        public DirectoryEntryHeader()
            : base(ZipHeaderType.DirectoryEntry)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            Version = reader.ReadUInt16();
            VersionNeededToExtract = reader.ReadUInt16();
            Flags = (HeaderFlags)reader.ReadUInt16();
            CompressionMethod = (ZipCompressionMethod)reader.ReadUInt16();
            LastModifiedTime = reader.ReadUInt16();
            LastModifiedDate = reader.ReadUInt16();
            Crc = reader.ReadUInt32();
            CompressedSize = reader.ReadUInt32();
            UncompressedSize = reader.ReadUInt32();
            ushort nameLength = reader.ReadUInt16();
            ushort extraLength = reader.ReadUInt16();
            ushort commentLength = reader.ReadUInt16();
            DiskNumberStart = reader.ReadUInt16();
            InternalFileAttributes = reader.ReadUInt16();
            ExternalFileAttributes = reader.ReadUInt32();
            RelativeOffsetOfEntryHeader = reader.ReadUInt32();

            byte[] name = reader.ReadBytes(nameLength);
            Name = DecodeString(name);
            byte[] extra = reader.ReadBytes(extraLength);
            byte[] comment = reader.ReadBytes(commentLength);
            Comment = DecodeString(comment);
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
                if (RelativeOffsetOfEntryHeader == uint.MaxValue)
                {
                    RelativeOffsetOfEntryHeader = zip64ExtraData.RelativeOffsetOfEntryHeader;
                }
            }
        }

        internal override void Write(BinaryWriter writer)
        {
			var zip64 = CompressedSize >= uint.MaxValue || UncompressedSize >= uint.MaxValue || RelativeOffsetOfEntryHeader >= uint.MaxValue;
			if (zip64)
				Version = (ushort)(Version > 45 ? Version : 45);

            writer.Write(Version);
            writer.Write(VersionNeededToExtract);
            writer.Write((ushort)Flags);
            writer.Write((ushort)CompressionMethod);
            writer.Write(LastModifiedTime);
            writer.Write(LastModifiedDate);
            writer.Write(Crc);
			writer.Write(zip64 ? uint.MaxValue : CompressedSize);
            writer.Write(zip64 ? uint.MaxValue : UncompressedSize);

            byte[] nameBytes = EncodeString(Name);
            writer.Write((ushort)nameBytes.Length);

			if (zip64)
			{
				writer.Write((ushort)(2 + 2 + 8 + 8 + 8 + 4));
			}
			else
			{
				//writer.Write((ushort)Extra.Length);
				writer.Write((ushort)0);
			}
            writer.Write((ushort)Comment.Length);

            writer.Write(DiskNumberStart);
            writer.Write(InternalFileAttributes);
            writer.Write(ExternalFileAttributes);
            writer.Write(zip64 ? uint.MaxValue : RelativeOffsetOfEntryHeader);

            writer.Write(nameBytes);

			if (zip64)
			{
				writer.Write((ushort)0x0001);
				writer.Write((ushort)((8 + 8 + 8 + 4)));

				writer.Write((ulong)UncompressedSize);
				writer.Write((ulong)CompressedSize);
				writer.Write((ulong)RelativeOffsetOfEntryHeader);
				writer.Write((uint)0); // VolumeNumber = 0
			}
            writer.Write(Comment);
        }

        internal ushort Version { get; private set; }

        public ushort VersionNeededToExtract { get; set; }

        public long RelativeOffsetOfEntryHeader { get; set; }

        public uint ExternalFileAttributes { get; set; }

        public ushort InternalFileAttributes { get; set; }

        public ushort DiskNumberStart { get; set; }

        public string Comment { get; private set; }
    }
}