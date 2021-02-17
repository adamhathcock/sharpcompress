using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Dmg.Headers
{
    internal sealed class BlkxTable : DmgStructBase
    {
        private const uint Signature = 0x6d697368u;

        public uint Version { get; }             // Current version is 1
        public ulong SectorNumber { get; }       // Starting disk sector in this blkx descriptor
        public ulong SectorCount { get; }        // Number of disk sectors in this blkx descriptor

        public ulong DataOffset { get; }
        public uint BuffersNeeded { get; }
        public uint BlockDescriptors { get; }    // Number of descriptors

        public UdifChecksum Checksum { get; }

        public IReadOnlyList<BlkxChunk> Chunks { get; }

        private BlkxTable(
            uint version,
            ulong sectorNumber,
            ulong sectorCount,
            ulong dataOffset,
            uint buffersNeeded,
            uint blockDescriptors,
            UdifChecksum checksum,
            IReadOnlyList<BlkxChunk> chunks)
        {
            Version = version;
            SectorNumber = sectorNumber;
            SectorCount = sectorCount;
            DataOffset = dataOffset;
            BuffersNeeded = buffersNeeded;
            BlockDescriptors = blockDescriptors;
            Checksum = checksum;
            Chunks = chunks;
        }

        public static bool TryRead(in byte[] buffer, out BlkxTable? header)
        {
            header = null;

            ReadOnlySpan<byte> data = buffer.AsSpan();

            uint sig = ReadUInt32(ref data);
            if (sig != Signature) return false;

            uint version = ReadUInt32(ref data);
            ulong sectorNumber = ReadUInt64(ref data);
            ulong sectorCount = ReadUInt64(ref data);

            ulong dataOffset = ReadUInt64(ref data);
            uint buffersNeeded = ReadUInt32(ref data);
            uint blockDescriptors = ReadUInt32(ref data);

            data = data.Slice(6 * sizeof(uint)); // reserved

            var checksum = UdifChecksum.Read(ref data);

            uint chunkCount = ReadUInt32(ref data);
            var chunks = new BlkxChunk[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                if (!BlkxChunk.TryRead(ref data, out var chunk)) return false;
                chunks[i] = chunk!;
            }

            header = new BlkxTable(version, sectorNumber, sectorCount, dataOffset, buffersNeeded, blockDescriptors, checksum, chunks);
            return true;
        }
    }
}
