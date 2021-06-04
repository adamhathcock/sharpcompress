using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSVolumeHeader : HFSStructBase
    {
        private const ushort SignaturePlus = 0x482B;
        private const ushort SignatureX = 0x4858;
        private const int FinderInfoCount = 8;

        public bool IsHFSX { get; }
        public ushort Version { get; }
        public uint Attributes { get; }
        public uint LastMountedVersion { get; }
        public uint JournalInfoBlock { get; }

        public DateTime CreateDate { get; }
        public DateTime ModifyDate { get; }
        public DateTime BackupDate { get; }
        public DateTime CheckedDate { get; }

        public uint FileCount { get; }
        public uint FolderCount { get; }

        public uint BlockSize { get; }
        public uint TotalBlocks { get; }
        public uint FreeBlocks { get; }

        public uint NextAllocation { get; }
        public uint RsrcClumpSize { get; }
        public uint DataClumpSize { get; }
        public uint NextCatalogID { get; }

        public uint WriteCount { get; }
        public ulong EncodingsBitmap { get; }

        public IReadOnlyList<uint> FinderInfo { get; }

        public HFSForkData AllocationFile { get; }
        public HFSForkData ExtentsFile { get; }
        public HFSForkData CatalogFile { get; }
        public HFSForkData AttributesFile { get; }
        public HFSForkData StartupFile { get; }

        public HFSVolumeHeader(
            bool isHFSX,
            ushort version,
            uint attributes,
            uint lastMountedVersion,
            uint journalInfoBlock,
            DateTime createDate,
            DateTime modifyDate,
            DateTime backupDate,
            DateTime checkedDate,
            uint fileCount,
            uint folderCount,
            uint blockSize,
            uint totalBlocks,
            uint freeBlocks,
            uint nextAllocation,
            uint rsrcClumpSize,
            uint dataClumpSize,
            uint nextCatalogID,
            uint writeCount,
            ulong encodingsBitmap,
            IReadOnlyList<uint> finderInfo,
            HFSForkData allocationFile,
            HFSForkData extentsFile,
            HFSForkData catalogFile,
            HFSForkData attributesFile,
            HFSForkData startupFile)
        {
            IsHFSX = isHFSX;
            Version = version;
            Attributes = attributes;
            LastMountedVersion = lastMountedVersion;
            JournalInfoBlock = journalInfoBlock;
            CreateDate = createDate;
            ModifyDate = modifyDate;
            BackupDate = backupDate;
            CheckedDate = checkedDate;
            FileCount = fileCount;
            FolderCount = folderCount;
            BlockSize = blockSize;
            TotalBlocks = totalBlocks;
            FreeBlocks = freeBlocks;
            NextAllocation = nextAllocation;
            RsrcClumpSize = rsrcClumpSize;
            DataClumpSize = dataClumpSize;
            NextCatalogID = nextCatalogID;
            WriteCount = writeCount;
            EncodingsBitmap = encodingsBitmap;
            FinderInfo = finderInfo;
            AllocationFile = allocationFile;
            ExtentsFile = extentsFile;
            CatalogFile = catalogFile;
            AttributesFile = attributesFile;
            StartupFile = startupFile;
        }

        private static IReadOnlyList<uint> ReadFinderInfo(Stream stream)
        {
            var finderInfo = new uint[FinderInfoCount];
            for (int i = 0; i < FinderInfoCount; i++)
                finderInfo[i] = ReadUInt32(stream);
            return finderInfo;
        }

        public static bool TryRead(Stream stream, out HFSVolumeHeader? header)
        {
            header = null;
            stream.Skip(1024); // reserved bytes

            bool isHFSX;
            ushort sig = ReadUInt16(stream);
            if (sig == SignaturePlus) isHFSX = false;
            else if (sig == SignatureX) isHFSX = true;
            else return false;

            ushort version = ReadUInt16(stream);
            uint attributes = ReadUInt32(stream);
            uint lastMountedVersion = ReadUInt32(stream);
            uint journalInfoBlock = ReadUInt32(stream);
            DateTime createDate = ReadDate(stream);
            DateTime modifyDate = ReadDate(stream);
            DateTime backupDate = ReadDate(stream);
            DateTime checkedDate = ReadDate(stream);
            uint fileCount = ReadUInt32(stream);
            uint folderCount = ReadUInt32(stream);
            uint blockSize = ReadUInt32(stream);
            uint totalBlocks = ReadUInt32(stream);
            uint freeBlocks = ReadUInt32(stream);
            uint nextAllocation = ReadUInt32(stream);
            uint rsrcClumpSize = ReadUInt32(stream);
            uint dataClumpSize = ReadUInt32(stream);
            uint nextCatalogID = ReadUInt32(stream);
            uint writeCount = ReadUInt32(stream);
            ulong encodingsBitmap = ReadUInt64(stream);
            IReadOnlyList<uint> finderInfo = ReadFinderInfo(stream);
            HFSForkData allocationFile = HFSForkData.Read(stream);
            HFSForkData extentsFile = HFSForkData.Read(stream);
            HFSForkData catalogFile = HFSForkData.Read(stream);
            HFSForkData attributesFile = HFSForkData.Read(stream);
            HFSForkData startupFile = HFSForkData.Read(stream);

            header = new HFSVolumeHeader(
                isHFSX,
                version,
                attributes,
                lastMountedVersion,
                journalInfoBlock,
                createDate,
                modifyDate,
                backupDate,
                checkedDate,
                fileCount,
                folderCount,
                blockSize,
                totalBlocks,
                freeBlocks,
                nextAllocation,
                rsrcClumpSize,
                dataClumpSize,
                nextCatalogID,
                writeCount,
                encodingsBitmap,
                finderInfo,
                allocationFile,
                extentsFile,
                catalogFile,
                attributesFile,
                startupFile);

            return true;
        }
    }
}
