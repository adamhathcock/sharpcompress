using System;
using System.Text;

namespace SharpCompress.Common.Zip.Headers
{
    internal enum ExtraDataType : ushort
    {
        WinZipAes = 0x9901,

        NotImplementedExtraData = 0xFFFF,
        // Third Party Mappings
        // -Info-ZIP Unicode Path Extra Field
        UnicodePathExtraField = 0x7075
    }

    internal class ExtraData
    {
        internal ExtraDataType Type { get; set; }
        internal ushort Length { get; set; }
        internal byte[] DataBytes { get; set; }
    }

    internal class ExtraUnicodePathExtraField : ExtraData
    {
        internal byte Version
        {
            get { return this.DataBytes[0]; }
        }

        internal byte[] NameCRC32
        {
            get
            {
                var crc = new byte[4];
                Buffer.BlockCopy(this.DataBytes, 1, crc, 0, 4);
                return crc;
            }
        }

        internal string UnicodeName
        {
            get
            {
                // PathNamelength = dataLength - Version(1 byte) - NameCRC32(4 bytes)
                var length = this.Length - 5;
                var nameStr = Encoding.UTF8.GetString(this.DataBytes, 5, length);
                return nameStr;
            }
        }
    }

    internal static class LocalEntryHeaderExtraFactory
    {
        internal static ExtraData Create(ExtraDataType type,ushort length, byte[] extraData)
        {
            switch (type)
            {
                case ExtraDataType.UnicodePathExtraField:
                    return new ExtraUnicodePathExtraField()
                    {
                        Type = type,
                        Length = length,
                        DataBytes = extraData
                    };
                default:
                    return new ExtraData
                    {
                        Type = type,
                        Length = length,
                        DataBytes = extraData
                    };
            }
        }
    }
}
