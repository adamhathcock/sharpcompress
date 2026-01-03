using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Arj.Headers
{
    public enum ArjHeaderType
    {
        MainHeader,
        LocalHeader,
    }

    public abstract class ArjHeader
    {
        private const int FIRST_HDR_SIZE = 34;
        private const ushort ARJ_MAGIC = 0xEA60;

        public ArjHeader(ArjHeaderType type)
        {
            ArjHeaderType = type;
        }

        public ArjHeaderType ArjHeaderType { get; }
        public byte Flags { get; set; }
        public FileType FileType { get; set; }

        public abstract ArjHeader? Read(Stream reader);

        public byte[] ReadHeader(Stream stream)
        {
            // check for magic bytes
            var magic = new byte[2];
            if (stream.Read(magic) != 2)
            {
                return Array.Empty<byte>();
            }

            if (!CheckMagicBytes(magic))
            {
                throw new InvalidDataException("Not an ARJ file (wrong magic bytes)");
            }

            // read header_size
            byte[] headerBytes = new byte[2];
            stream.Read(headerBytes, 0, 2);
            var headerSize = (ushort)(headerBytes[0] | headerBytes[1] << 8);
            if (headerSize < 1)
            {
                return Array.Empty<byte>();
            }

            var body = new byte[headerSize];
            var read = stream.Read(body, 0, headerSize);
            if (read < headerSize)
            {
                return Array.Empty<byte>();
            }

            byte[] crc = new byte[4];
            read = stream.Read(crc, 0, 4);
            var checksum = Crc32Stream.Compute(body);
            // Compute the hash value
            if (checksum != BitConverter.ToUInt32(crc, 0))
            {
                throw new InvalidDataException("Header checksum is invalid");
            }
            return body;
        }

        protected List<byte[]> ReadExtendedHeaders(Stream reader)
        {
            List<byte[]> extendedHeader = new List<byte[]>();
            byte[] buffer = new byte[2];

            while (true)
            {
                int bytesRead = reader.Read(buffer, 0, 2);
                if (bytesRead < 2)
                {
                    throw new EndOfStreamException(
                        "Unexpected end of stream while reading extended header size."
                    );
                }

                var extHeaderSize = (ushort)(buffer[0] | (buffer[1] << 8));
                if (extHeaderSize == 0)
                {
                    return extendedHeader;
                }

                byte[] header = new byte[extHeaderSize];
                bytesRead = reader.Read(header, 0, extHeaderSize);
                if (bytesRead < extHeaderSize)
                {
                    throw new EndOfStreamException(
                        "Unexpected end of stream while reading extended header data."
                    );
                }

                byte[] crc = new byte[4];
                bytesRead = reader.Read(crc, 0, 4);
                if (bytesRead < 4)
                {
                    throw new EndOfStreamException(
                        "Unexpected end of stream while reading extended header CRC."
                    );
                }

                var checksum = Crc32Stream.Compute(header);
                if (checksum != BitConverter.ToUInt32(crc, 0))
                {
                    throw new InvalidDataException("Extended header checksum is invalid");
                }

                extendedHeader.Add(header);
            }
        }

        // Flag helpers
        public bool IsGabled => (Flags & 0x01) != 0;
        public bool IsAnsiPage => (Flags & 0x02) != 0;
        public bool IsVolume => (Flags & 0x04) != 0;
        public bool IsArjProtected => (Flags & 0x08) != 0;
        public bool IsPathSym => (Flags & 0x10) != 0;
        public bool IsBackup => (Flags & 0x20) != 0;
        public bool IsSecured => (Flags & 0x40) != 0;
        public bool IsAltName => (Flags & 0x80) != 0;

        public static FileType FileTypeFromByte(byte value)
        {
            return Enum.IsDefined(typeof(FileType), value)
                ? (FileType)value
                : Headers.FileType.Unknown;
        }

        public static bool IsArchive(Stream stream)
        {
            var bytes = new byte[2];
            if (stream.Read(bytes, 0, 2) != 2)
            {
                return false;
            }

            return CheckMagicBytes(bytes);
        }

        protected static bool CheckMagicBytes(byte[] headerBytes)
        {
            var magicValue = (ushort)(headerBytes[0] | headerBytes[1] << 8);
            return magicValue == ARJ_MAGIC;
        }
    }
}
