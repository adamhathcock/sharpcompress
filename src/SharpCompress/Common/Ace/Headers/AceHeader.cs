using System;
using System.IO;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Ace.Headers
{
    /// <summary>
    /// Header type constants
    /// </summary>
    public enum AceHeaderType
    {
        MAIN = 0,
        FILE = 1,
        RECOVERY32 = 2,
        RECOVERY64A = 3,
        RECOVERY64B = 4,
    }

    public abstract class AceHeader
    {
        // ACE signature: bytes at offset 7 should be "**ACE**"
        private static readonly byte[] AceSignature =
        [
            (byte)'*',
            (byte)'*',
            (byte)'A',
            (byte)'C',
            (byte)'E',
            (byte)'*',
            (byte)'*',
        ];

        public AceHeader(IArchiveEncoding archiveEncoding, AceHeaderType type)
        {
            AceHeaderType = type;
            ArchiveEncoding = archiveEncoding;
        }

        public IArchiveEncoding ArchiveEncoding { get; }
        public AceHeaderType AceHeaderType { get; }

        public ushort HeaderFlags { get; set; }
        public ushort HeaderCrc { get; set; }
        public ushort HeaderSize { get; set; }
        public byte HeaderType { get; set; }

        public bool IsFileEncrypted =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.FILE_ENCRYPTED) != 0;
        public bool Is64Bit =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.MEMORY_64BIT) != 0;

        public bool IsSolid =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.SOLID_MAIN) != 0;

        public bool IsMultiVolume =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.MULTIVOLUME) != 0;

        public abstract AceHeader? Read(Stream reader);

        public byte[] ReadHeader(Stream stream)
        {
            // Read header CRC (2 bytes) and header size (2 bytes)
            var headerBytes = new byte[4];
            if (stream.Read(headerBytes, 0, 4) != 4)
            {
                return Array.Empty<byte>();
            }

            HeaderCrc = BitConverter.ToUInt16(headerBytes, 0); // CRC for validation
            HeaderSize = BitConverter.ToUInt16(headerBytes, 2);
            if (HeaderSize == 0)
            {
                return Array.Empty<byte>();
            }

            // Read the header data
            var body = new byte[HeaderSize];
            if (stream.Read(body, 0, HeaderSize) != HeaderSize)
            {
                return Array.Empty<byte>();
            }

            // Verify crc
            var checksum = AceCrc.AceCrc16(body);
            if (checksum != HeaderCrc)
            {
                throw new InvalidDataException("Header checksum is invalid");
            }
            return body;
        }

        public static bool IsArchive(Stream stream)
        {
            // ACE files have a specific signature
            // First two bytes are typically 0x60 0xEA (signature bytes)
            // At offset 7, there should be "**ACE**" (7 bytes)
            var bytes = new byte[14];
            if (stream.Read(bytes, 0, 14) != 14)
            {
                return false;
            }

            // Check for "**ACE**" at offset 7
            return CheckMagicBytes(bytes, 7);
        }

        protected static bool CheckMagicBytes(byte[] headerBytes, int offset)
        {
            // Check for "**ACE**" at specified offset
            for (int i = 0; i < AceSignature.Length; i++)
            {
                if (headerBytes[offset + i] != AceSignature[i])
                {
                    return false;
                }
            }
            return true;
        }

        protected DateTime ConvertDosDateTime(uint dosDateTime)
        {
            try
            {
                int second = (int)(dosDateTime & 0x1F) * 2;
                int minute = (int)((dosDateTime >> 5) & 0x3F);
                int hour = (int)((dosDateTime >> 11) & 0x1F);
                int day = (int)((dosDateTime >> 16) & 0x1F);
                int month = (int)((dosDateTime >> 21) & 0x0F);
                int year = (int)((dosDateTime >> 25) & 0x7F) + 1980;

                if (
                    day < 1
                    || day > 31
                    || month < 1
                    || month > 12
                    || hour > 23
                    || minute > 59
                    || second > 59
                )
                {
                    return DateTime.MinValue;
                }

                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
