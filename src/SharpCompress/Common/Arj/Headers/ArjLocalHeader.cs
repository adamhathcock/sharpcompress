using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers
{
    public class ArjLocalHeader : ArjHeader
    {
        public ArchiveEncoding ArchiveEncoding { get; }
        public long DataStartPosition { get; protected set; }

        public byte ArchiverVersionNumber { get; set; }
        public byte MinVersionToExtract { get; set; }
        public HostOS HostOS { get; set; }
        public CompressionMethod CompressionMethod { get; set; }
        public DosDateTime DateTimeModified { get; set; } = new DosDateTime(0);
        public long CompressedSize { get; set; }
        public long OriginalSize { get; set; }
        public long OriginalCrc32 { get; set; }
        public int FileSpecPosition { get; set; }
        public int FileAccessMode { get; set; }
        public byte FirstChapter { get; set; }
        public byte LastChapter { get; set; }
        public long ExtendedFilePosition { get; set; }
        public DosDateTime DateTimeAccessed { get; set; } = new DosDateTime(0);
        public DosDateTime DateTimeCreated { get; set; } = new DosDateTime(0);
        public long OriginalSizeEvenForVolumes { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        private const byte StdHdrSize = 30;
        private const byte R9HdrSize = 46;

        public ArjLocalHeader(ArchiveEncoding archiveEncoding)
            : base(ArjHeaderType.LocalHeader)
        {
            ArchiveEncoding =
                archiveEncoding ?? throw new ArgumentNullException(nameof(archiveEncoding));
        }

        public override ArjHeader? Read(Stream stream)
        {
            var body = ReadHeader(stream);
            if (body.Length > 0)
            {
                ReadExtendedHeaders(stream);
                var header = LoadFrom(body);
                header.DataStartPosition = stream.Position;
                return header;
            }
            return null;
        }

        public ArjLocalHeader LoadFrom(byte[] headerBytes)
        {
            int offset = 0;

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

            byte headerSize = headerBytes[offset++];
            ArchiverVersionNumber = headerBytes[offset++];
            MinVersionToExtract = headerBytes[offset++];
            HostOS hostOS = (HostOS)headerBytes[offset++];
            Flags = headerBytes[offset++];
            CompressionMethod = CompressionMethodFromByte(headerBytes[offset++]);
            FileType = FileTypeFromByte(headerBytes[offset++]);

            offset++; // Skip 1 byte

            var rawTimestamp = ReadInt32();
            DateTimeModified =
                rawTimestamp != 0 ? new DosDateTime(rawTimestamp) : new DosDateTime(0);

            CompressedSize = ReadInt32();
            OriginalSize = ReadInt32();
            OriginalCrc32 = ReadInt32();
            FileSpecPosition = ReadInt16();
            FileAccessMode = ReadInt16();

            FirstChapter = headerBytes[offset++];
            LastChapter = headerBytes[offset++];

            ExtendedFilePosition = 0;
            OriginalSizeEvenForVolumes = 0;

            if (headerSize > StdHdrSize)
            {
                ExtendedFilePosition = ReadInt32();

                if (headerSize >= R9HdrSize)
                {
                    rawTimestamp = ReadInt32();
                    DateTimeAccessed =
                        rawTimestamp != 0 ? new DosDateTime(rawTimestamp) : new DosDateTime(0);
                    rawTimestamp = ReadInt32();
                    DateTimeCreated =
                        rawTimestamp != 0 ? new DosDateTime(rawTimestamp) : new DosDateTime(0);
                    OriginalSizeEvenForVolumes = ReadInt32();
                }
            }

            Name = Encoding.ASCII.GetString(
                headerBytes,
                offset,
                Array.IndexOf(headerBytes, (byte)0, offset) - offset
            );
            offset += Name.Length + 1;

            Comment = Encoding.ASCII.GetString(
                headerBytes,
                offset,
                Array.IndexOf(headerBytes, (byte)0, offset) - offset
            );
            offset += Comment.Length + 1;

            return this;
        }

        public static CompressionMethod CompressionMethodFromByte(byte value)
        {
            return value switch
            {
                0 => CompressionMethod.Stored,
                1 => CompressionMethod.CompressedMost,
                2 => CompressionMethod.Compressed,
                3 => CompressionMethod.CompressedFaster,
                4 => CompressionMethod.CompressedFastest,
                8 => CompressionMethod.NoDataNoCrc,
                9 => CompressionMethod.NoData,
                _ => CompressionMethod.Unknown,
            };
        }
    }
}
