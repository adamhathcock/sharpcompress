using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal abstract class HFSTreeNode : HFSStructBase
    {
        private static byte[]? _buffer = null;

        public HFSTreeNodeDescriptor Descriptor { get; }

        protected HFSTreeNode(HFSTreeNodeDescriptor descriptor)
            => Descriptor = descriptor;

        public static bool TryRead(Stream stream, HFSTreeHeaderRecord headerRecord, bool isHFSX, out HFSTreeNode? node)
        {
            node = null;

            if (!HFSTreeNodeDescriptor.TryRead(stream, out var descriptor)) return false;

            int size = (int)headerRecord.NodeSize - HFSTreeNodeDescriptor.Size;
            if ((_buffer is null) || (_buffer.Length < size))
                _buffer = new byte[size * 2];

            if (stream.Read(_buffer, 0, size) != size)
                throw new EndOfStreamException();
            ReadOnlySpan<byte> data = _buffer.AsSpan(0, size);

            switch (descriptor!.Kind)
            {
                case HFSTreeNodeKind.Leaf:
                    node = HFSLeafTreeNode.Read(descriptor, data, headerRecord, isHFSX);
                    return true;

                case HFSTreeNodeKind.Index:
                    node = HFSIndexTreeNode.Read(descriptor, data, headerRecord, isHFSX);
                    return true;

                case HFSTreeNodeKind.Map:
                    node = HFSMapTreeNode.Read(descriptor, data);
                    return true;
            }

            return false;
        }
    }

    internal sealed class HFSHeaderTreeNode : HFSTreeNode
    {
        private const int UserDataSize = 128;

        public HFSTreeHeaderRecord HeaderRecord { get; }

        public IReadOnlyList<byte> UserData { get; }

        public IReadOnlyList<byte> Map { get; }

        private HFSHeaderTreeNode(
            HFSTreeNodeDescriptor descriptor,
            HFSTreeHeaderRecord headerRecord,
            IReadOnlyList<byte> userData,
            IReadOnlyList<byte> map)
            : base(descriptor)
        {
            HeaderRecord = headerRecord;
            UserData = userData;
            Map = map;
        }

        public static HFSHeaderTreeNode Read(HFSTreeNodeDescriptor descriptor, Stream stream)
        {
            if (descriptor.Kind != HFSTreeNodeKind.Header)
                throw new ArgumentException("Descriptor does not define a header node");

            var headerRecord = HFSTreeHeaderRecord.Read(stream);
            var userData = new byte[UserDataSize];
            if (stream.Read(userData, 0, UserDataSize) != UserDataSize)
                throw new EndOfStreamException();

            int mapSize = (int)(headerRecord.NodeSize - 256);
            var map = new byte[mapSize];
            if (stream.Read(map, 0, mapSize) != mapSize)
                throw new EndOfStreamException();

            // offset values (not required for header node)
            _ = ReadUInt16(stream);
            _ = ReadUInt16(stream);
            _ = ReadUInt16(stream);
            _ = ReadUInt16(stream);

            return new HFSHeaderTreeNode(descriptor, headerRecord, userData, map);
        }
    }

    internal sealed class HFSMapTreeNode : HFSTreeNode
    {
        public IReadOnlyList<byte> Map { get; }

        private HFSMapTreeNode(HFSTreeNodeDescriptor descriptor, IReadOnlyList<byte> map)
            : base(descriptor)
        {
            Map = map;
        }

        public static HFSMapTreeNode Read(HFSTreeNodeDescriptor descriptor, ReadOnlySpan<byte> data)
        {
            int mapSize = data.Length - 6;
            var map = new byte[mapSize];
            data.Slice(0, mapSize).CopyTo(map);

            return new HFSMapTreeNode(descriptor, map);
        }
    }

    internal sealed class HFSIndexTreeNode : HFSTreeNode
    {
        public IReadOnlyList<HFSPointerRecord> Records { get; }

        private HFSIndexTreeNode(HFSTreeNodeDescriptor descriptor, IReadOnlyList<HFSPointerRecord> records)
            : base(descriptor)
        {
            Records = records;
        }

        public static HFSIndexTreeNode Read(HFSTreeNodeDescriptor descriptor, ReadOnlySpan<byte> data, HFSTreeHeaderRecord headerRecord, bool isHFSX)
        {
            int recordCount = descriptor.NumRecords;
            var records = new HFSPointerRecord[recordCount];
            for (int i = 0; i < recordCount; i++)
                records[i] = HFSPointerRecord.Read(ref data, headerRecord, isHFSX);
            return new HFSIndexTreeNode(descriptor, records);
        }
    }

    internal sealed class HFSLeafTreeNode : HFSTreeNode
    {
        public IReadOnlyList<HFSDataRecord> Records { get; }

        private HFSLeafTreeNode(HFSTreeNodeDescriptor descriptor, IReadOnlyList<HFSDataRecord> records)
            : base(descriptor)
        {
            Records = records;
        }

        public static HFSLeafTreeNode Read(HFSTreeNodeDescriptor descriptor, ReadOnlySpan<byte> data, HFSTreeHeaderRecord headerRecord, bool isHFSX)
        {
            int recordCount = descriptor.NumRecords;
            var recordOffsets = new int[recordCount + 1];
            for (int i = 0; i < recordOffsets.Length; i++)
            {
                var offsetData = data.Slice(data.Length - (2 * i) - 2);
                ushort offset = ReadUInt16(ref offsetData);
                recordOffsets[i] = offset;
            }

            var records = new HFSDataRecord[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                int size = recordOffsets[i + 1] - recordOffsets[i];
                records[i] = HFSDataRecord.Read(ref data, size, headerRecord, isHFSX);
            }

            return new HFSLeafTreeNode(descriptor, records);
        }
    }
}
