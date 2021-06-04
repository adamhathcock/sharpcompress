using System;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSCatalogKey : HFSStructBase, IEquatable<HFSCatalogKey>, IComparable<HFSCatalogKey>, IComparable
    {
        private readonly StringComparer _comparer;

        public uint ParentId { get; }

        public string Name { get; }

        private static StringComparer GetComparer(HFSKeyCompareType compareType, bool isHFSX)
        {
            if (isHFSX)
            {
                return compareType switch
                {
                    HFSKeyCompareType.CaseFolding => StringComparer.InvariantCultureIgnoreCase,
                    HFSKeyCompareType.BinaryCompare => StringComparer.Ordinal,
                    _ => StringComparer.InvariantCultureIgnoreCase
                };
            }
            else
            {
                return StringComparer.InvariantCultureIgnoreCase;
            }
        }

        public HFSCatalogKey(uint parentId, string name, HFSKeyCompareType compareType, bool isHFSX)
        {
            ParentId = parentId;
            Name = name;
            _comparer = GetComparer(compareType, isHFSX);
        }

        public HFSCatalogKey(byte[] key, HFSKeyCompareType compareType, bool isHFSX)
        {
            ReadOnlySpan<byte> data = key.AsSpan();
            ParentId = ReadUInt32(ref data);
            Name = ReadString(ref data, true);
            _comparer = GetComparer(compareType, isHFSX);
        }

        public bool Equals(HFSCatalogKey? other)
        {
            if (other is null) return false;
            else return (ParentId == other.ParentId) && _comparer.Equals(Name, other.Name);
        }

        public override bool Equals(object? obj)
        {
            if (obj is HFSCatalogKey other) return Equals(other);
            else return false;
        }

        public int CompareTo(HFSCatalogKey? other)
        {
            if (other is null) return 1;

            int result = ParentId.CompareTo(other.ParentId);
            if (result == 0) result = _comparer.Compare(Name, other.Name);
            return result;
        }

        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
            else if (obj is HFSCatalogKey other) return CompareTo(other);
            else throw new ArgumentException("Object is not of type CatalogKey", nameof(obj));
        }

        public override int GetHashCode()
            => ParentId.GetHashCode() ^ _comparer.GetHashCode(Name);

        public static bool operator ==(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return right is null;
            else return left.Equals(right);
        }

        public static bool operator !=(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return right is not null;
            else return !left.Equals(right);
        }

        public static bool operator <(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return right is not null;
            else return left.CompareTo(right) < 0;
        }

        public static bool operator >(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return false;
            else return left.CompareTo(right) > 0;
        }

        public static bool operator <=(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return true;
            else return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(HFSCatalogKey? left, HFSCatalogKey? right)
        {
            if (left is null) return right is null;
            else return left.CompareTo(right) >= 0;
        }
    }

    internal enum HFSCatalogRecordType : ushort
    {
        Folder = 0x0001,
        File = 0x0002,
        FolderThread = 0x0003,
        FileThread = 0x0004
    }

    internal abstract class HFSCatalogRecord : HFSStructBase
    {
        public HFSCatalogRecordType Type { get; }

        protected HFSCatalogRecord(HFSCatalogRecordType type)
            => Type = type;

        public static bool TryRead(ref ReadOnlySpan<byte> data, HFSKeyCompareType compareType, bool isHFSX, out HFSCatalogRecord? record)
        {
            record = null;

            ushort rawType = ReadUInt16(ref data);
            if (!Enum.IsDefined(typeof(HFSCatalogRecordType), rawType)) return false;

            var type = (HFSCatalogRecordType)rawType;
            switch (type)
            {
                case HFSCatalogRecordType.Folder:
                    record = HFSCatalogFolder.Read(ref data);
                    return true;

                case HFSCatalogRecordType.File:
                    record = HFSCatalogFile.Read(ref data);
                    return true;

                case HFSCatalogRecordType.FolderThread:
                    record = HFSCatalogThread.Read(ref data, false, compareType, isHFSX);
                    return true;

                case HFSCatalogRecordType.FileThread:
                    record = HFSCatalogThread.Read(ref data, true, compareType, isHFSX);
                    return true;
            }

            return false;
        }
    }

    internal sealed class HFSCatalogFolder : HFSCatalogRecord
    {
        public uint Valence { get; }
        public uint FolderId { get; }
        public DateTime CreateDate { get; }
        public DateTime ContentModDate { get; }
        public DateTime AttributeModDate { get; }
        public DateTime AccessDate { get; }
        public DateTime BackupDate { get; }
        public HFSPermissions Permissions { get; }
        public HFSFolderInfo Info { get; }
        public uint TextEncoding { get; }

        private HFSCatalogFolder(
            uint valence,
            uint folderId,
            DateTime createDate,
            DateTime contentModDate,
            DateTime attributeModDate,
            DateTime accessDate,
            DateTime backupDate,
            HFSPermissions permissions,
            HFSFolderInfo info,
            uint textEncoding)
            : base(HFSCatalogRecordType.Folder)
        {
            Valence = valence;
            FolderId = folderId;
            CreateDate = createDate;
            ContentModDate = contentModDate;
            AttributeModDate = attributeModDate;
            AccessDate = accessDate;
            BackupDate = backupDate;
            Permissions = permissions;
            Info = info;
            TextEncoding = textEncoding;
        }

        public static HFSCatalogFolder Read(ref ReadOnlySpan<byte> data)
        {
            _ = ReadUInt16(ref data); // reserved
            uint valence = ReadUInt32(ref data);
            uint folderId = ReadUInt32(ref data);
            var createDate = ReadDate(ref data);
            var contentModDate = ReadDate(ref data);
            var attributeModDate = ReadDate(ref data);
            var accessDate = ReadDate(ref data);
            var backupDate = ReadDate(ref data);
            var permissions = HFSPermissions.Read(ref data);
            var info = HFSFolderInfo.Read(ref data);
            uint textEncoding = ReadUInt32(ref data);
            _ = ReadUInt32(ref data); // reserved

            return new HFSCatalogFolder(
                valence,
                folderId,
                createDate,
                contentModDate,
                attributeModDate,
                accessDate,
                backupDate,
                permissions,
                info,
                textEncoding);
        }
    }

    internal enum HFSFileFlags : ushort
    {
        LockedBit = 0x0000,
        LockedMask = 0x0001,
        ThreadExistsBit = 0x0001,
        ThreadExistsMask = 0x0002
    }

    internal sealed class HFSCatalogFile : HFSCatalogRecord
    {
        public HFSFileFlags Flags { get; }
        public uint FileId { get; }
        public DateTime CreateDate { get; }
        public DateTime ContentModDate { get; }
        public DateTime AttributeModDate { get; }
        public DateTime AccessDate { get; }
        public DateTime BackupDate { get; }
        public HFSPermissions Permissions { get; }
        public HFSFileInfo Info { get; }
        public uint TextEncoding { get; }

        public HFSForkData DataFork { get; }
        public HFSForkData ResourceFork { get; }

        private HFSCatalogFile(
            HFSFileFlags flags,
            uint fileId,
            DateTime createDate,
            DateTime contentModDate,
            DateTime attributeModDate,
            DateTime accessDate,
            DateTime backupDate,
            HFSPermissions permissions,
            HFSFileInfo info,
            uint textEncoding,
            HFSForkData dataFork,
            HFSForkData resourceFork)
            :base(HFSCatalogRecordType.File)
        {
            Flags = flags;
            FileId = fileId;
            CreateDate = createDate;
            ContentModDate = contentModDate;
            AttributeModDate = attributeModDate;
            AccessDate = accessDate;
            BackupDate = backupDate;
            Permissions = permissions;
            Info = info;
            TextEncoding = textEncoding;
            DataFork = dataFork;
            ResourceFork = resourceFork;
        }

        public static HFSCatalogFile Read(ref ReadOnlySpan<byte> data)
        {
            var flags = (HFSFileFlags)ReadUInt16(ref data);
            _ = ReadUInt32(ref data); // reserved
            uint fileId = ReadUInt32(ref data);
            var createDate = ReadDate(ref data);
            var contentModDate = ReadDate(ref data);
            var attributeModDate = ReadDate(ref data);
            var accessDate = ReadDate(ref data);
            var backupDate = ReadDate(ref data);
            var permissions = HFSPermissions.Read(ref data);
            var info = HFSFileInfo.Read(ref data);
            uint textEncoding = ReadUInt32(ref data);
            _ = ReadUInt32(ref data); // reserved

            var dataFork = HFSForkData.Read(ref data);
            var resourceFork = HFSForkData.Read(ref data);

            return new HFSCatalogFile(
                flags,
                fileId,
                createDate,
                contentModDate,
                attributeModDate,
                accessDate,
                backupDate,
                permissions,
                info,
                textEncoding,
                dataFork,
                resourceFork);
        }
    }

    internal sealed class HFSCatalogThread : HFSCatalogRecord
    {
        public uint ParentId { get; }
        public string NodeName { get; }
        public HFSCatalogKey CatalogKey { get; }

        private HFSCatalogThread(uint parentId, string nodeName, bool isFile, HFSKeyCompareType compareType, bool isHFSX)
            : base(isFile ? HFSCatalogRecordType.FileThread : HFSCatalogRecordType.FolderThread)
        {
            ParentId = parentId;
            NodeName = nodeName;
            CatalogKey = new HFSCatalogKey(ParentId, NodeName, compareType, isHFSX);
        }

        public static HFSCatalogThread Read(ref ReadOnlySpan<byte> data, bool isFile, HFSKeyCompareType compareType, bool isHFSX)
        {
            _ = ReadInt16(ref data); // reserved
            uint parentId = ReadUInt32(ref data);
            string nodeName = ReadString(ref data, true);

            return new HFSCatalogThread(parentId, nodeName, isFile, compareType, isHFSX);
        }
    }
}
