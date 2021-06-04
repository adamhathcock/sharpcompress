using System;

namespace SharpCompress.Common.Dmg.HFS
{
    internal abstract class HFSKeyedRecord : HFSStructBase
    {
        private readonly HFSKeyCompareType _compareType;
        private readonly bool _isHFSX;
        private HFSCatalogKey? _catalogKey;
        private HFSExtentKey? _extentKey;

        public byte[] Key { get; }

        public HFSCatalogKey GetCatalogKey() => _catalogKey ??= new HFSCatalogKey(Key, _compareType, _isHFSX);

        public HFSExtentKey GetExtentKey() => _extentKey ??= new HFSExtentKey(Key);

        protected HFSKeyedRecord(byte[] key, HFSKeyCompareType compareType, bool isHFSX)
        {
            Key = key;
            _compareType = compareType;
            _isHFSX = isHFSX;
        }
    }

    internal sealed class HFSPointerRecord : HFSKeyedRecord
    {
        public uint NodeNumber { get; }

        private HFSPointerRecord(byte[] key, uint nodeNumber, HFSKeyCompareType compareType, bool isHFSX)
            : base(key, compareType, isHFSX)
        {
            NodeNumber = nodeNumber;
        }

        public static HFSPointerRecord Read(ref ReadOnlySpan<byte> data, HFSTreeHeaderRecord headerRecord, bool isHFSX)
        {
            bool isBigKey = headerRecord.Attributes.HasFlag(HFSTreeAttributes.BigKeys);
            ushort keyLength = isBigKey ? ReadUInt16(ref data) : ReadUInt8(ref data);
            if (!headerRecord.Attributes.HasFlag(HFSTreeAttributes.VariableIndexKeys)) keyLength = headerRecord.MaxKeyLength;
            int keySize = (isBigKey ? 2 : 1) + keyLength;

            var key = new byte[keyLength];
            data.Slice(0, keyLength).CopyTo(key);
            data = data.Slice(keyLength);

            // data is always aligned to 2 bytes
            if (keySize % 2 == 1) data = data.Slice(1);

            uint nodeNumber = ReadUInt32(ref data);

            return new HFSPointerRecord(key, nodeNumber, headerRecord.KeyCompareType, isHFSX);
        }
    }

    internal sealed class HFSDataRecord : HFSKeyedRecord
    {
        public byte[] Data { get; }

        private HFSDataRecord(byte[] key, byte[] data, HFSKeyCompareType compareType, bool isHFSX)
            : base(key, compareType, isHFSX)
        {
            Data = data;
        }

        public static HFSDataRecord Read(ref ReadOnlySpan<byte> data, int size, HFSTreeHeaderRecord headerRecord, bool isHFSX)
        {
            bool isBigKey = headerRecord.Attributes.HasFlag(HFSTreeAttributes.BigKeys);
            ushort keyLength = isBigKey ? ReadUInt16(ref data) : ReadUInt8(ref data);
            int keySize = (isBigKey ? 2 : 1) + keyLength;
            size -= keySize;

            var key = new byte[keyLength];
            data.Slice(0, keyLength).CopyTo(key);
            data = data.Slice(keyLength);

            // data is always aligned to 2 bytes
            if (keySize % 2 == 1)
            {
                data = data.Slice(1);
                size--;
            }

            var structData = new byte[size];
            data.Slice(0, size).CopyTo(structData);
            data = data.Slice(size);

            return new HFSDataRecord(key, structData, headerRecord.KeyCompareType, isHFSX);
        }
    }
}
