using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSForkData : HFSStructBase
    {
        private const int ExtentCount = 8;

        public ulong LogicalSize { get; }
        public uint ClumpSize { get; }
        public uint TotalBlocks { get; }
        public IReadOnlyList<HFSExtentDescriptor> Extents { get; }

        private HFSForkData(ulong logicalSize, uint clumpSize, uint totalBlocks, IReadOnlyList<HFSExtentDescriptor> extents)
        {
            LogicalSize = logicalSize;
            ClumpSize = clumpSize;
            TotalBlocks = totalBlocks;
            Extents = extents;
        }

        public static HFSForkData Read(Stream stream)
        {
            ulong logicalSize = ReadUInt64(stream);
            uint clumpSize = ReadUInt32(stream);
            uint totalBlocks = ReadUInt32(stream);

            var extents = new HFSExtentDescriptor[ExtentCount];
            for (int i = 0; i < ExtentCount; i++)
                extents[i] = HFSExtentDescriptor.Read(stream);

            return new HFSForkData(logicalSize, clumpSize, totalBlocks, extents);
        }

        public static HFSForkData Read(ref ReadOnlySpan<byte> data)
        {
            ulong logicalSize = ReadUInt64(ref data);
            uint clumpSize = ReadUInt32(ref data);
            uint totalBlocks = ReadUInt32(ref data);

            var extents = new HFSExtentDescriptor[ExtentCount];
            for (int i = 0; i < ExtentCount; i++)
                extents[i] = HFSExtentDescriptor.Read(ref data);

            return new HFSForkData(logicalSize, clumpSize, totalBlocks, extents);
        }
    }
}
