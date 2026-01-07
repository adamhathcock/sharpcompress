using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.Common.Arc
{
    public class ArcEntryHeader
    {
        public IArchiveEncoding ArchiveEncoding { get; }
        public CompressionType CompressionMethod { get; private set; }
        public string? Name { get; private set; }
        public long CompressedSize { get; private set; }
        public DateTime DateTime { get; private set; }
        public int Crc16 { get; private set; }
        public long OriginalSize { get; private set; }
        public long DataStartPosition { get; private set; }

        public ArcEntryHeader(IArchiveEncoding archiveEncoding)
        {
            this.ArchiveEncoding = archiveEncoding;
        }

        public ArcEntryHeader? ReadHeader(Stream stream)
        {
            byte[] headerBytes = new byte[29];
            if (stream.Read(headerBytes, 0, headerBytes.Length) != headerBytes.Length)
            {
                return null;
            }
            DataStartPosition = stream.Position;
            return LoadFrom(headerBytes);
        }

        public ArcEntryHeader LoadFrom(byte[] headerBytes)
        {
            CompressionMethod = GetCompressionType(headerBytes[1]);

            // Read name
            int nameEnd = Array.IndexOf(headerBytes, (byte)0, 1); // Find null terminator
            Name = Encoding.UTF8.GetString(headerBytes, 2, nameEnd > 0 ? nameEnd - 2 : 12);

            int offset = 15;
            CompressedSize = BitConverter.ToUInt32(headerBytes, offset);
            offset += 4;
            uint rawDateTime = BitConverter.ToUInt32(headerBytes, offset);
            DateTime = ConvertToDateTime(rawDateTime);
            offset += 4;
            Crc16 = BitConverter.ToUInt16(headerBytes, offset);
            offset += 2;
            OriginalSize = BitConverter.ToUInt32(headerBytes, offset);
            return this;
        }

        private CompressionType GetCompressionType(byte value)
        {
            return value switch
            {
                1 or 2 => CompressionType.None,
                3 => CompressionType.Packed,
                4 => CompressionType.Squeezed,
                5 or 6 or 7 or 8 => CompressionType.Crunched,
                9 => CompressionType.Squashed,
                10 => CompressionType.Crushed,
                11 => CompressionType.Distilled,
                _ => CompressionType.Unknown,
            };
        }

        public static DateTime ConvertToDateTime(long rawDateTime)
        {
            // Convert Unix timestamp to DateTime (UTC)
            return DateTimeOffset.FromUnixTimeSeconds(rawDateTime).UtcDateTime;
        }
    }
}
