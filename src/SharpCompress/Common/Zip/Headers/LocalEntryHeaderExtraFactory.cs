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
        }

        // From the spec, values are only in the extradata if the standard
        // value is set to 0xFFFFFFFF (or 0xFFFF for the Disk Start Number).
        // Values, if present, must appear in the following order:
        // - Original Size
        // - Compressed Size
        // - Relative Header Offset
        // - Disk Start Number
        public void Process(long uncompressedFileSize, long compressedFileSize, long relativeHeaderOffset, ushort diskNumber)
        {
            var bytesRequired = ((uncompressedFileSize == uint.MaxValue) ? 8 : 0)
                + ((compressedFileSize == uint.MaxValue) ? 8 : 0)
                + ((relativeHeaderOffset == uint.MaxValue) ? 8 : 0)
                + ((diskNumber == ushort.MaxValue) ? 4 : 0);
            var currentIndex = 0;

            if (bytesRequired > DataBytes.Length)
            {
                throw new ArchiveException("Zip64 extended information extra field is not large enough for the required information");
            }

            if (uncompressedFileSize == uint.MaxValue)
            {
                UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(currentIndex));
                currentIndex += 8;
            }

            if (compressedFileSize == uint.MaxValue)
            {
                CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(currentIndex));
                currentIndex += 8;
            }

            if (relativeHeaderOffset == uint.MaxValue)
            {
                RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(currentIndex));
                currentIndex += 8;
            }

            if (diskNumber == ushort.MaxValue)
            {
                VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes.AsSpan(currentIndex));
            }
        }

        /// <summary>
        /// Uncompressed file size. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
        /// original entry header had a corresponding 0xFFFFFFFF value.
        /// </summary>
        public long UncompressedSize { get; private set; }

        /// <summary>
        /// Compressed file size. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
        /// original entry header had a corresponding 0xFFFFFFFF value.
        /// </summary>
        public long CompressedSize { get; private set; }

        /// <summary>
        /// Relative offset of the entry header. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
        /// original entry header had a corresponding 0xFFFFFFFF value.
        /// </summary>
        public long RelativeOffsetOfEntryHeader { get; private set; }

        /// <summary>
        /// Volume number. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
        /// original entry header had a corresponding 0xFFFF value.
        /// </summary>
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
