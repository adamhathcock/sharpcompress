using System;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal enum HFSTreeNodeKind : sbyte
    {
        Leaf = -1,
        Index = 0,
        Header = 1,
        Map = 2
    }

    internal sealed class HFSTreeNodeDescriptor : HFSStructBase
    {
        public const int Size = 14;

        public uint FLink { get; }
        public uint BLink { get; }
        public HFSTreeNodeKind Kind { get; }
        public byte Height { get; }
        public ushort NumRecords { get; }

        private HFSTreeNodeDescriptor(uint fLink, uint bLink, HFSTreeNodeKind kind, byte height, ushort numRecords)
        {
            FLink = fLink;
            BLink = bLink;
            Kind = kind;
            Height = height;
            NumRecords = numRecords;
        }

        public static bool TryRead(Stream stream, out HFSTreeNodeDescriptor? descriptor)
        {
            descriptor = null;

            uint fLink = ReadUInt32(stream);
            uint bLink = ReadUInt32(stream);

            sbyte rawKind = (sbyte)ReadUInt8(stream);
            if (!Enum.IsDefined(typeof(HFSTreeNodeKind), rawKind)) return false;
            var kind = (HFSTreeNodeKind)rawKind;

            byte height = ReadUInt8(stream);
            if (((kind == HFSTreeNodeKind.Header) || (kind == HFSTreeNodeKind.Map)) && (height != 0)) return false;
            if ((kind == HFSTreeNodeKind.Leaf) && (height != 1)) return false;

            ushort numRecords = ReadUInt16(stream);
            _ = ReadUInt16(stream); // reserved

            descriptor = new HFSTreeNodeDescriptor(fLink, bLink, kind, height, numRecords);
            return true;
        }
    }
}
