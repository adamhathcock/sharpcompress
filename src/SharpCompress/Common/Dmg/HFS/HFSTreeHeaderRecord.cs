using System;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal enum HFSTreeType : byte
    {
        HFS = 0,         // control file
        User = 128,      // user btree type starts from 128
        Reserved = 255
    }

    internal enum HFSKeyCompareType : byte
    {
        CaseFolding = 0xCF,	  // case-insensitive
        BinaryCompare = 0xBC  // case-sensitive
    }

    [Flags]
    internal enum HFSTreeAttributes : uint
    {
        None = 0x00000000,
        BadClose = 0x00000001,
        BigKeys = 0x00000002,
        VariableIndexKeys = 0x00000004
    }

    internal sealed class HFSTreeHeaderRecord : HFSStructBase
    {
        public ushort TreeDepth;
        public uint RootNode;
        public uint LeafRecords;
        public uint FirstLeafNode;
        public uint LastLeafNode;
        public ushort NodeSize;
        public ushort MaxKeyLength;
        public uint TotalNodes;
        public uint FreeNodes;
        public uint ClumpSize;
        public HFSTreeType TreeType;
        public HFSKeyCompareType KeyCompareType;
        public HFSTreeAttributes Attributes;

        private HFSTreeHeaderRecord(
            ushort treeDepth,
            uint rootNode,
            uint leafRecords,
            uint firstLeafNode,
            uint lastLeafNode,
            ushort nodeSize,
            ushort maxKeyLength,
            uint totalNodes,
            uint freeNodes,
            uint clumpSize,
            HFSTreeType treeType,
            HFSKeyCompareType keyCompareType,
            HFSTreeAttributes attributes)
        {
            TreeDepth = treeDepth;
            RootNode = rootNode;
            LeafRecords = leafRecords;
            FirstLeafNode = firstLeafNode;
            LastLeafNode = lastLeafNode;
            NodeSize = nodeSize;
            MaxKeyLength = maxKeyLength;
            TotalNodes = totalNodes;
            FreeNodes = freeNodes;
            ClumpSize = clumpSize;
            TreeType = treeType;
            KeyCompareType = keyCompareType;
            Attributes = attributes;
        }

        public static HFSTreeHeaderRecord Read(Stream stream)
        {
            ushort treeDepth = ReadUInt16(stream);
            uint rootNode = ReadUInt32(stream);
            uint leafRecords = ReadUInt32(stream);
            uint firstLeafNode = ReadUInt32(stream);
            uint lastLeafNode = ReadUInt32(stream);
            ushort nodeSize = ReadUInt16(stream);
            ushort maxKeyLength = ReadUInt16(stream);
            uint totalNodes = ReadUInt32(stream);
            uint freeNodes = ReadUInt32(stream);
            _ = ReadUInt16(stream); // reserved
            uint clumpSize = ReadUInt32(stream);
            var treeType = (HFSTreeType)ReadUInt8(stream);
            var keyCompareType = (HFSKeyCompareType)ReadUInt8(stream);
            var attributes = (HFSTreeAttributes)ReadUInt32(stream);
            for (int i = 0; i < 16; i++) _ = ReadUInt32(stream); // reserved

            return new HFSTreeHeaderRecord(
                treeDepth,
                rootNode,
                leafRecords,
                firstLeafNode,
                lastLeafNode,
                nodeSize,
                maxKeyLength,
                totalNodes,
                freeNodes,
                clumpSize,
                treeType,
                keyCompareType,
                attributes);
        }
    }
}
