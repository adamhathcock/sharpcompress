using System;

namespace SharpCompress.Common.Dmg.Headers
{
    internal enum BlkxChunkType : uint
    {
        Zero = 0x00000000u,
        Uncompressed = 0x00000001u,
        Ignore = 0x00000002u,
        AdcCompressed = 0x80000004u,
        ZlibCompressed = 0x80000005u,
        Bz2Compressed = 0x80000006u,
        Comment = 0x7FFFFFFEu,
        Last = 0xFFFFFFFFu,
    }

    internal sealed class BlkxChunk : DmgStructBase
    {
        private const int SectorSize = 512;

        public BlkxChunkType Type { get; }      // Compression type used or chunk type
        public uint Comment { get; }            // "+beg" or "+end", if EntryType is comment (0x7FFFFFFE). Else reserved.
        public ulong UncompressedOffset { get; }      // Start sector of this chunk
        public ulong UncompressedLength { get; }       // Number of sectors in this chunk
        public ulong CompressedOffset { get; }  // Start of chunk in data fork
        public ulong CompressedLength { get; }  // Count of bytes of chunk, in data fork

        private BlkxChunk(BlkxChunkType type, uint comment, ulong sectorNumber, ulong sectorCount, ulong compressedOffset, ulong compressedLength)
        {
            Type = type;
            Comment = comment;
            UncompressedOffset = sectorNumber * SectorSize;
            UncompressedLength = sectorCount * SectorSize;
            CompressedOffset = compressedOffset;
            CompressedLength = compressedLength;
        }

        public static bool TryRead(ref ReadOnlySpan<byte> data, out BlkxChunk? chunk)
        {
            chunk = null;

            var type = (BlkxChunkType)ReadUInt32(ref data);
            if (!Enum.IsDefined(typeof(BlkxChunkType), type)) return false;

            chunk = new BlkxChunk(type, ReadUInt32(ref data), ReadUInt64(ref data), ReadUInt64(ref data), ReadUInt64(ref data), ReadUInt64(ref data));
            return true;
        }
    }
}
