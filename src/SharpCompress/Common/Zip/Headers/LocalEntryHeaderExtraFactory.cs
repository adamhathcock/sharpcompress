using System;
using System.Buffers.Binary;
using System.Text;

namespace SharpCompress.Common.Zip.Headers
{
    internal enum ExtraDataType : ushort
    {
        WinZipAes = 0x9901,

        NotImplementedExtraData = 0xFFFF,

        // Third Party Mappings
        // -Info-ZIP Unicode Path Extra Field
        UnicodePathExtraField = 0x7075,
        Zip64ExtendedInformationExtraField = 0x0001
    }

    internal class ExtraData
    {
        public ExtraData(ExtraDataType type, ushort length, byte[] dataBytes)
        {
            Type = type;
            Length = length;
            DataBytes = dataBytes;
        }

        internal ExtraDataType Type { get; }
        internal ushort Length { get; }
        internal byte[] DataBytes { get; }
    }

    internal sealed class ExtraUnicodePathExtraField : ExtraData
    {
        public ExtraUnicodePathExtraField(ExtraDataType type, ushort length, byte[] dataBytes)
            : base(type, length, dataBytes)
        {
        }

        internal byte Version => DataBytes[0];

        internal byte[] NameCrc32
        {
            get
            {
                var crc = new byte[4];
                Buffer.BlockCopy(DataBytes, 1, crc, 0, 4);
                return crc;
            }
        }

        internal string UnicodeName
        {
            get
            {
                // PathNamelength = dataLength - Version(1 byte) - NameCRC32(4 bytes)
                var length = Length - 5;
                var nameStr = Encoding.UTF8.GetString(DataBytes, 5, length);
                return nameStr;
            }
        }
    }

    internal sealed class Zip64ExtendedInformationExtraField : ExtraData
    {
        public Zip64ExtendedInformationExtraField(ExtraDataType type, ushort length, byte[] dataBytes)
            : base(type, length, dataBytes)
        {
            Process();
        }

        //From the spec values are only in the extradata if the standard
        //value is set to 0xFFFF, but if one of the sizes are present, both are.
        //Hence if length == 4 volume only
        //      if length == 8 offset only
        //      if length == 12 offset + volume
        //      if length == 16 sizes only
        //      if length == 20 sizes + volume
        //      if length == 24 sizes + offset
        //      if length == 28 everything.
        //It is unclear how many of these are used in the wild.

        private void Process()
        {
            switch (DataBytes.Length)
            {
                case 4:
                    VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes);
                    return;
                case 8:
                    RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    return;
                case 12:
                    RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes.AsSpan(8));
                    return;
                case 16:
                    UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(8));
                    return;
                case 20:
                    UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(8));
                    VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes.AsSpan(16));
                    return;
                case 24:
                    UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(8));
                    RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(16));
                    return;
                case 28:
                    UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes);
                    CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(8));
                    RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(16));
                    VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes.AsSpan(24));
                    return;
                default:
                    throw new ArchiveException("Unexpected size of of Zip64 extended information extra field");
            }
        }

        public long UncompressedSize { get; private set; }
        public long CompressedSize { get; private set; }
        public long RelativeOffsetOfEntryHeader { get; private set; }
        public uint VolumeNumber { get; private set; }
    }

    internal static class LocalEntryHeaderExtraFactory
    {
        internal static ExtraData Create(ExtraDataType type, ushort length, byte[] extraData)
        {
            return type switch
            {
                ExtraDataType.UnicodePathExtraField => new ExtraUnicodePathExtraField(type, length, extraData),
                ExtraDataType.Zip64ExtendedInformationExtraField => new Zip64ExtendedInformationExtraField(type, length, extraData),
                _ => new ExtraData(type, length, extraData)
            };
        }
    }
}
