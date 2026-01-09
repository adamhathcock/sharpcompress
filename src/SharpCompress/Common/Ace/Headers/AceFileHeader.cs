using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using SharpCompress.Common.Arc;

namespace SharpCompress.Common.Ace.Headers
{
    /// <summary>
    /// ACE file entry header
    /// </summary>
    public sealed class AceFileHeader : AceHeader
    {
        public long DataStartPosition { get; private set; }
        public long PackedSize { get; set; }
        public long OriginalSize { get; set; }
        public DateTime DateTime { get; set; }
        public int Attributes { get; set; }
        public uint Crc32 { get; set; }
        public CompressionType CompressionType { get; set; }
        public CompressionQuality CompressionQuality { get; set; }
        public ushort Parameters { get; set; }
        public string Filename { get; set; } = string.Empty;
        public List<byte> Comment { get; set; } = new();

        /// <summary>
        /// File data offset in the archive
        /// </summary>
        public ulong DataOffset { get; set; }

        public bool IsDirectory => (Attributes & 0x10) != 0;

        public bool IsContinuedFromPrev =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.CONTINUED_PREV) != 0;

        public bool IsContinuedToNext =>
            (HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.CONTINUED_NEXT) != 0;

        public int DictionarySize
        {
            get
            {
                int bits = Parameters & 0x0F;
                return bits < 10 ? 1024 : 1 << bits;
            }
        }

        public AceFileHeader(IArchiveEncoding archiveEncoding)
            : base(archiveEncoding, AceHeaderType.FILE) { }

        /// <summary>
        /// Reads the next file entry header from the stream.
        /// Returns null if no more entries or end of archive.
        /// Supports both ACE 1.0 and ACE 2.0 formats.
        /// </summary>
        public override AceHeader? Read(Stream stream)
        {
            var headerData = ReadHeader(stream);
            if (headerData.Length == 0)
            {
                return null;
            }
            int offset = 0;

            // Header type (1 byte)
            HeaderType = headerData[offset++];

            // Skip recovery record headers (ACE 2.0 feature)
            if (HeaderType == (byte)SharpCompress.Common.Ace.Headers.AceHeaderType.RECOVERY32)
            {
                // Skip to next header
                return null;
            }

            if (HeaderType != (byte)SharpCompress.Common.Ace.Headers.AceHeaderType.FILE)
            {
                // Unknown header type - skip
                return null;
            }

            // Header flags (2 bytes)
            HeaderFlags = BitConverter.ToUInt16(headerData, offset);
            offset += 2;

            // Packed size (4 bytes)
            PackedSize = BitConverter.ToUInt32(headerData, offset);
            offset += 4;

            // Original size (4 bytes)
            OriginalSize = BitConverter.ToUInt32(headerData, offset);
            offset += 4;

            // File date/time in DOS format (4 bytes)
            var dosDateTime = BitConverter.ToUInt32(headerData, offset);
            DateTime = ConvertDosDateTime(dosDateTime);
            offset += 4;

            // File attributes (4 bytes)
            Attributes = (int)BitConverter.ToUInt32(headerData, offset);
            offset += 4;

            // CRC32 (4 bytes)
            Crc32 = BitConverter.ToUInt32(headerData, offset);
            offset += 4;

            // Compression type (1 byte)
            byte compressionType = headerData[offset++];
            CompressionType = GetCompressionType(compressionType);

            // Compression quality/parameter (1 byte)
            byte compressionQuality = headerData[offset++];
            CompressionQuality = GetCompressionQuality(compressionQuality);

            // Parameters (2 bytes)
            Parameters = BitConverter.ToUInt16(headerData, offset);
            offset += 2;

            // Reserved (2 bytes) - skip
            offset += 2;

            // Filename length (2 bytes)
            var filenameLength = BitConverter.ToUInt16(headerData, offset);
            offset += 2;

            // Filename
            if (offset + filenameLength <= headerData.Length)
            {
                Filename = ArchiveEncoding.Decode(headerData, offset, filenameLength);
                offset += filenameLength;
            }

            // Handle comment if present
            if ((HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.COMMENT) != 0)
            {
                // Comment length (2 bytes)
                if (offset + 2 <= headerData.Length)
                {
                    ushort commentLength = BitConverter.ToUInt16(headerData, offset);
                    offset += 2 + commentLength; // Skip comment
                }
            }

            // Store the data start position
            DataStartPosition = stream.Position;

            return this;
        }

        public CompressionType GetCompressionType(byte value) =>
            value switch
            {
                0 => CompressionType.Stored,
                1 => CompressionType.Lz77,
                2 => CompressionType.Blocked,
                _ => CompressionType.Unknown,
            };

        public CompressionQuality GetCompressionQuality(byte value) =>
            value switch
            {
                0 => CompressionQuality.None,
                1 => CompressionQuality.Fastest,
                2 => CompressionQuality.Fast,
                3 => CompressionQuality.Normal,
                4 => CompressionQuality.Good,
                5 => CompressionQuality.Best,
                _ => CompressionQuality.Unknown,
            };
    }
}
