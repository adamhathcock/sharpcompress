using System;
using System.IO;
using System.Text;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Arj.Headers
{
    public class ArjMainHeader : ArjHeader
    {
        private const int FIRST_HDR_SIZE = 34;
        private const ushort ARJ_MAGIC = 0xEA60;

        public ArchiveEncoding ArchiveEncoding { get; }

        public int ArchiverVersionNumber { get; private set; }
        public int MinVersionToExtract { get; private set; }
        public HostOS HostOs { get; private set; }
        public int SecurityVersion { get; private set; }
        public DosDateTime CreationDateTime { get; private set; } = new DosDateTime(0);
        public long CompressedSize { get; private set; }
        public long ArchiveSize { get; private set; }
        public long SecurityEnvelope { get; private set; }
        public int FileSpecPosition { get; private set; }
        public int SecurityEnvelopeLength { get; private set; }
        public int EncryptionVersion { get; private set; }
        public int LastChapter { get; private set; }

        public int ArjProtectionFactor { get; private set; }
        public int Flags2 { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Comment { get; private set; } = string.Empty;

        public ArjMainHeader(ArchiveEncoding archiveEncoding)
            : base(ArjHeaderType.MainHeader)
        {
            ArchiveEncoding =
                archiveEncoding ?? throw new ArgumentNullException(nameof(archiveEncoding));
        }

        public override ArjHeader? Read(Stream stream)
        {
            var body = ReadHeader(stream);
            ReadExtendedHeaders(stream);
            return LoadFrom(body);
        }

        public ArjMainHeader LoadFrom(byte[] headerBytes)
        {
            var offset = 1;

            byte ReadByte()
            {
                if (offset >= headerBytes.Length)
                {
                    throw new EndOfStreamException();
                }
                return (byte)(headerBytes[offset++] & 0xFF);
            }

            int ReadInt16()
            {
                if (offset + 1 >= headerBytes.Length)
                {
                    throw new EndOfStreamException();
                }
                var v = headerBytes[offset] & 0xFF | (headerBytes[offset + 1] & 0xFF) << 8;
                offset += 2;
                return v;
            }

            long ReadInt32()
            {
                if (offset + 3 >= headerBytes.Length)
                {
                    throw new EndOfStreamException();
                }
                long v =
                    headerBytes[offset] & 0xFF
                    | (headerBytes[offset + 1] & 0xFF) << 8
                    | (headerBytes[offset + 2] & 0xFF) << 16
                    | (headerBytes[offset + 3] & 0xFF) << 24;
                offset += 4;
                return v;
            }
            string ReadNullTerminatedString(byte[] x, int startIndex)
            {
                var result = new StringBuilder();
                int i = startIndex;

                while (i < x.Length && x[i] != 0)
                {
                    result.Append((char)x[i]);
                    i++;
                }

                // Skip the null terminator
                i++;
                if (i < x.Length)
                {
                    byte[] remainder = new byte[x.Length - i];
                    Array.Copy(x, i, remainder, 0, remainder.Length);
                    x = remainder;
                }

                return result.ToString();
            }

            ArchiverVersionNumber = ReadByte();
            MinVersionToExtract = ReadByte();

            var hostOsByte = ReadByte();
            HostOs = hostOsByte <= 11 ? (HostOS)hostOsByte : HostOS.Unknown;

            Flags = ReadByte();
            SecurityVersion = ReadByte();
            FileType = FileTypeFromByte(ReadByte());

            offset++; // skip reserved

            CreationDateTime = new DosDateTime((int)ReadInt32());
            CompressedSize = ReadInt32();
            ArchiveSize = ReadInt32();

            SecurityEnvelope = ReadInt32();
            FileSpecPosition = ReadInt16();
            SecurityEnvelopeLength = ReadInt16();

            EncryptionVersion = ReadByte();
            LastChapter = ReadByte();

            Name = ReadNullTerminatedString(headerBytes, offset);
            Comment = ReadNullTerminatedString(headerBytes, offset + 1 + Name.Length);

            return this;
        }
    }
}
