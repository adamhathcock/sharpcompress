using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Dmg.Headers
{
    internal sealed class DmgHeader : DmgStructBase
    {
        public const int HeaderSize = 512;
        private const uint Signature = 0x6B6F6C79u;
        private const int UuidSize = 16; // 128 bit

        public uint Version { get; }                     // Current version is 4
        public uint Flags { get; }                       // Flags
        public ulong RunningDataForkOffset { get; }      //
        public ulong DataForkOffset { get; }             // Data fork offset (usually 0, beginning of file)
        public ulong DataForkLength { get; }             // Size of data fork (usually up to the XMLOffset, below)
        public ulong RsrcForkOffset { get; }             // Resource fork offset, if any
        public ulong RsrcForkLength { get; }             // Resource fork length, if any
        public uint SegmentNumber { get; }               // Usually 1, may be 0
        public uint SegmentCount { get; }                // Usually 1, may be 0
        public IReadOnlyList<byte> SegmentID { get; }    // 128-bit GUID identifier of segment (if SegmentNumber !=0)

        public UdifChecksum DataChecksum { get; }

        public ulong XMLOffset { get; }                  // Offset of property list in DMG, from beginning
        public ulong XMLLength { get; }                  // Length of property list

        public UdifChecksum Checksum { get; }
        
        public uint ImageVariant { get; }                // Commonly 1
        public ulong SectorCount { get; }                // Size of DMG when expanded, in sectors

        private DmgHeader(
            uint version,
            uint flags,
            ulong runningDataForkOffset,
            ulong dataForkOffset,
            ulong dataForkLength,
            ulong rsrcForkOffset,
            ulong rsrcForkLength,
            uint segmentNumber,
            uint segmentCount,
            IReadOnlyList<byte> segmentID,
            UdifChecksum dataChecksum,
            ulong xMLOffset,
            ulong xMLLength,
            UdifChecksum checksum,
            uint imageVariant,
            ulong sectorCount)
        {
            Version = version;
            Flags = flags;
            RunningDataForkOffset = runningDataForkOffset;
            DataForkOffset = dataForkOffset;
            DataForkLength = dataForkLength;
            RsrcForkOffset = rsrcForkOffset;
            RsrcForkLength = rsrcForkLength;
            SegmentNumber = segmentNumber;
            SegmentCount = segmentCount;
            SegmentID = segmentID;
            DataChecksum = dataChecksum;
            XMLOffset = xMLOffset;
            XMLLength = xMLLength;
            Checksum = checksum;
            ImageVariant = imageVariant;
            SectorCount = sectorCount;
        }

        private static void ReadUuid(ref ReadOnlySpan<byte> data, byte[] buffer)
        {
            data.Slice(0, UuidSize).CopyTo(buffer);
            data = data.Slice(UuidSize);
        }

        internal static bool TryRead(Stream input, out DmgHeader? header)
        {
            header = null;

            var buffer = new byte[HeaderSize];
            int count = input.Read(buffer, 0, HeaderSize);
            if (count != HeaderSize) return false;
            ReadOnlySpan<byte> data = buffer.AsSpan();

            uint sig = ReadUInt32(ref data);
            if (sig != Signature) return false;

            uint version = ReadUInt32(ref data);

            uint size = ReadUInt32(ref data);
            if (size != (uint)HeaderSize) return false;

            uint flags = ReadUInt32(ref data);
            ulong runningDataForkOffset = ReadUInt64(ref data);
            ulong dataForkOffset = ReadUInt64(ref data);
            ulong dataForkLength = ReadUInt64(ref data);
            ulong rsrcForkOffset = ReadUInt64(ref data);
            ulong rsrcForkLength = ReadUInt64(ref data);
            uint segmentNumber = ReadUInt32(ref data);
            uint segmentCount = ReadUInt32(ref data);

            var segmentID = new byte[UuidSize];
            ReadUuid(ref data, segmentID);

            var dataChecksum = UdifChecksum.Read(ref data);

            ulong xmlOffset = ReadUInt64(ref data);
            ulong xmlLength = ReadUInt64(ref data);

            data = data.Slice(120); // Reserved bytes

            var checksum = UdifChecksum.Read(ref data);

            uint imageVariant = ReadUInt32(ref data);
            ulong sectorCount = ReadUInt64(ref data);

            header = new DmgHeader(
                version,
                flags,
                runningDataForkOffset,
                dataForkOffset,
                dataForkLength,
                rsrcForkOffset,
                rsrcForkLength,
                segmentNumber,
                segmentCount,
                segmentID,
                dataChecksum,
                xmlOffset,
                xmlLength,
                checksum,
                imageVariant,
                sectorCount);

            return true;
        }
    }
}
