using SharpCompress.Archives.Dmg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Dmg.HFS
{
    internal static class HFSUtil
    {
        private const string CorruptHFSMessage = "Corrupt HFS volume";

        private static (HFSHeaderTreeNode, IReadOnlyList<HFSTreeNode>) ReadTree(Stream stream, bool isHFSX)
        {
            if (!HFSTreeNodeDescriptor.TryRead(stream, out var headerDesc))
                throw new InvalidFormatException(CorruptHFSMessage);
            var header = HFSHeaderTreeNode.Read(headerDesc!, stream);

            var nodes = new HFSTreeNode[header.HeaderRecord.TotalNodes];
            nodes[0] = header;

            for (int i = 1; i < nodes.Length; i++)
            {
                if (!HFSTreeNode.TryRead(stream, header.HeaderRecord, isHFSX, out var node))
                    throw new InvalidFormatException(CorruptHFSMessage);

                nodes[i] = node!;
            }

            return (header, nodes);
        }

        private static void EnumerateExtentsTree(
            IReadOnlyList<HFSTreeNode> extentsTree,
            IDictionary<HFSExtentKey, HFSExtentRecord> records,
            int parentIndex)
        {
            var parent = extentsTree[parentIndex];
            if (parent is HFSLeafTreeNode leafNode)
            {
                foreach (var record in leafNode.Records)
                {
                    ReadOnlySpan<byte> data = record.Data.AsSpan();
                    var recordData = HFSExtentRecord.Read(ref data);
                    var key = record.GetExtentKey();
                    records.Add(key, recordData);
                }
            }
            else if (parent is HFSIndexTreeNode indexNode)
            {
                foreach (var record in indexNode.Records)
                    EnumerateExtentsTree(extentsTree, records, (int)record.NodeNumber);
            }
            else
            {
                throw new InvalidFormatException(CorruptHFSMessage);
            }
        }

        private static IReadOnlyDictionary<HFSExtentKey, HFSExtentRecord> LoadExtents(IReadOnlyList<HFSTreeNode> extentsTree, int rootIndex)
        {
            var records = new Dictionary<HFSExtentKey, HFSExtentRecord>();
            if (rootIndex == 0) return records;

            EnumerateExtentsTree(extentsTree, records, rootIndex);
            return records;
        }

        private static void EnumerateCatalogTree(
            HFSHeaderTreeNode catalogHeader,
            IReadOnlyList<HFSTreeNode> catalogTree,
            IDictionary<HFSCatalogKey, HFSCatalogRecord> records,
            IDictionary<uint, HFSCatalogThread> threads,
            int parentIndex,
            bool isHFSX)
        {
            var parent = catalogTree[parentIndex];
            if (parent is HFSLeafTreeNode leafNode)
            {
                foreach (var record in leafNode.Records)
                {
                    ReadOnlySpan<byte> data = record.Data.AsSpan();
                    if (HFSCatalogRecord.TryRead(ref data, catalogHeader.HeaderRecord.KeyCompareType, isHFSX, out var recordData))
                    {
                        var key = record.GetCatalogKey();
                        if ((recordData!.Type == HFSCatalogRecordType.FileThread) || (recordData!.Type == HFSCatalogRecordType.FolderThread))
                        {
                            threads.Add(key.ParentId, (HFSCatalogThread)recordData);
                        }
                        else
                        {
                            records.Add(key, recordData);
                        }
                    }
                    else
                    {
                        throw new InvalidFormatException(CorruptHFSMessage);
                    }
                }
            }
            else if (parent is HFSIndexTreeNode indexNode)
            {
                foreach (var record in indexNode.Records)
                    EnumerateCatalogTree(catalogHeader, catalogTree, records, threads, (int)record.NodeNumber, isHFSX);
            }
            else
            {
                throw new InvalidFormatException(CorruptHFSMessage);
            }
        }

        private static (HFSCatalogKey, HFSCatalogRecord) GetRecord(uint id, IDictionary<HFSCatalogKey, HFSCatalogRecord> records, IDictionary<uint, HFSCatalogThread> threads)
        {
            if (threads.TryGetValue(id, out var thread))
            {
                if (records.TryGetValue(thread.CatalogKey, out var record))
                    return (thread.CatalogKey, record!);
            }

            throw new InvalidFormatException(CorruptHFSMessage);
        }

        private static string SanitizePath(string path)
        {
            var sb = new StringBuilder(path.Length);
            foreach (char c in path)
            {
                if (!char.IsControl(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string GetPath(HFSCatalogKey key, IDictionary<HFSCatalogKey, HFSCatalogRecord> records, IDictionary<uint, HFSCatalogThread> threads)
        {
            if (key.ParentId == 1)
            {
                return key.Name;
            }
            else
            {
                var (parentKey, _) = GetRecord(key.ParentId, records, threads);
                var path = Path.Combine(GetPath(parentKey, records, threads), key.Name);
                return SanitizePath(path);
            }
        }

        private static IEnumerable<DmgArchiveEntry> LoadEntriesFromCatalogTree(
            Stream partitionStream,
            DmgFilePart filePart,
            HFSVolumeHeader volumeHeader,
            HFSHeaderTreeNode catalogHeader,
            IReadOnlyList<HFSTreeNode> catalogTree,
            IReadOnlyDictionary<HFSExtentKey, HFSExtentRecord> extents,
            DmgArchive archive,
            int rootIndex)
        {
            if (rootIndex == 0) return Array.Empty<DmgArchiveEntry>();

            var records = new Dictionary<HFSCatalogKey, HFSCatalogRecord>();
            var threads = new Dictionary<uint, HFSCatalogThread>();
            EnumerateCatalogTree(catalogHeader, catalogTree, records, threads, rootIndex, volumeHeader.IsHFSX);

            var entries = new List<DmgArchiveEntry>();
            foreach (var kvp in records)
            {
                var key = kvp.Key;
                var record = kvp.Value;

                string path = GetPath(key, records, threads);
                var stream = (record is HFSCatalogFile file) ? new HFSForkStream(partitionStream, volumeHeader, file.DataFork, file.FileId, extents) : null;
                var entry = new DmgArchiveEntry(stream, archive, record, path, filePart);
                entries.Add(entry);
            }

            return entries;
        }

        public static IEnumerable<DmgArchiveEntry> LoadEntriesFromPartition(Stream partitionStream, string fileName, DmgArchive archive)
        {
            if (!HFSVolumeHeader.TryRead(partitionStream, out var volumeHeader))
                throw new InvalidFormatException(CorruptHFSMessage);
            var filePart = new DmgFilePart(partitionStream, fileName);

            var extentsFile = volumeHeader!.ExtentsFile;
            var extentsStream = new HFSForkStream(partitionStream, volumeHeader, extentsFile);
            var (extentsHeader, extentsTree) = ReadTree(extentsStream, volumeHeader.IsHFSX);

            var extents = LoadExtents(extentsTree, (int)extentsHeader.HeaderRecord.RootNode);

            var catalogFile = volumeHeader!.CatalogFile;
            var catalogStream = new HFSForkStream(partitionStream, volumeHeader, catalogFile);
            var (catalogHeader, catalogTree) = ReadTree(catalogStream, volumeHeader.IsHFSX);

            return LoadEntriesFromCatalogTree(
                partitionStream,
                filePart,
                volumeHeader,
                catalogHeader,
                catalogTree,
                extents,
                archive,
                (int)catalogHeader.HeaderRecord.RootNode);
        }
    }
}
