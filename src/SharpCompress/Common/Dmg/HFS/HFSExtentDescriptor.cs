using System;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSExtentDescriptor : HFSStructBase
    {
        public uint StartBlock { get; }
        public uint BlockCount { get; }

        private HFSExtentDescriptor(uint startBlock, uint blockCount)
        {
            StartBlock = startBlock;
            BlockCount = blockCount;
        }

        public static HFSExtentDescriptor Read(Stream stream)
        {
            return new HFSExtentDescriptor(
                ReadUInt32(stream),
                ReadUInt32(stream));
        }

        public static HFSExtentDescriptor Read(ref ReadOnlySpan<byte> data)
        {
            return new HFSExtentDescriptor(
                ReadUInt32(ref data),
                ReadUInt32(ref data));
        }
    }
}
