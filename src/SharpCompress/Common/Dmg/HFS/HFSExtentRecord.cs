using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSExtentKey : HFSStructBase, IEquatable<HFSExtentKey>, IComparable<HFSExtentKey>, IComparable
    {
        public byte ForkType { get; }
        public uint FileId { get; }
        public uint StartBlock { get; }

        public HFSExtentKey(byte forkType, uint fileId, uint startBlock)
        {
            ForkType = forkType;
            FileId = fileId;
            StartBlock = startBlock;
        }

        public HFSExtentKey(byte[] key)
        {
            ReadOnlySpan<byte> data = key.AsSpan();
            ForkType = ReadUInt8(ref data);
            _ = ReadUInt8(ref data); // padding
            FileId = ReadUInt32(ref data);
            StartBlock = ReadUInt32(ref data);
        }

        public bool Equals(HFSExtentKey? other)
        {
            if (other is null) return false;
            else return (ForkType == other.ForkType) && (FileId == other.FileId) && (StartBlock == other.StartBlock);
        }

        public override bool Equals(object? obj)
        {
            if (obj is HFSExtentKey other) return Equals(other);
            else return false;
        }

        public int CompareTo(HFSExtentKey? other)
        {
            if (other is null) return 1;

            int result = FileId.CompareTo(other.FileId);
            if (result == 0) result = ForkType.CompareTo(other.ForkType);
            if (result == 0) result = StartBlock.CompareTo(other.StartBlock);
            return result;
        }

        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
            else if (obj is HFSExtentKey other) return CompareTo(other);
            else throw new ArgumentException("Object is not of type ExtentKey", nameof(obj));
        }

        public override int GetHashCode()
            => ForkType.GetHashCode() ^ FileId.GetHashCode() ^ StartBlock.GetHashCode();

        public static bool operator ==(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return right is null;
            else return left.Equals(right);
        }

        public static bool operator !=(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return right is not null;
            else return !left.Equals(right);
        }

        public static bool operator <(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return right is not null;
            else return left.CompareTo(right) < 0;
        }

        public static bool operator >(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return false;
            else return left.CompareTo(right) > 0;
        }

        public static bool operator <=(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return true;
            else return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(HFSExtentKey? left, HFSExtentKey? right)
        {
            if (left is null) return right is null;
            else return left.CompareTo(right) >= 0;
        }
    }

    internal sealed class HFSExtentRecord : HFSStructBase
    {
        private const int ExtentCount = 8;

        public IReadOnlyList<HFSExtentDescriptor> Extents { get; }

        private HFSExtentRecord(IReadOnlyList<HFSExtentDescriptor> extents)
            => Extents = extents;

        public static HFSExtentRecord Read(ref ReadOnlySpan<byte> data)
        {
            var extents = new HFSExtentDescriptor[ExtentCount];
            for (int i = 0; i < ExtentCount; i++)
                extents[i] = HFSExtentDescriptor.Read(ref data);

            return new HFSExtentRecord(extents);
        }
    }
}
