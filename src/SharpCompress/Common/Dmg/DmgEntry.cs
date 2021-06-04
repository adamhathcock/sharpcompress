using SharpCompress.Common.Dmg.HFS;
using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Dmg
{
    public abstract class DmgEntry : Entry
    {
        public override string Key { get; }
        public override bool IsDirectory { get; }
        public override long Size { get; }
        public override long CompressedSize { get; }
        public override CompressionType CompressionType { get; }
        public override DateTime? LastModifiedTime { get; }
        public override DateTime? CreatedTime { get; }
        public override DateTime? LastAccessedTime { get; }
        public override DateTime? ArchivedTime { get; }
        
        public override long Crc { get; } = 0; // Not stored
        public override string? LinkTarget { get; } = null;
        public override bool IsEncrypted { get; } = false;
        public override bool IsSplitAfter { get; } = false;

        internal override IEnumerable<FilePart> Parts { get; }

        internal DmgEntry(HFSCatalogRecord record, string path, long size, DmgFilePart part)
        {
            Key = path;
            IsDirectory = record.Type == HFSCatalogRecordType.Folder;
            Size = CompressedSize = size;              // There is no way to get the actual compressed size or the compression type of
            CompressionType = CompressionType.Unknown; // a file in a DMG archive since the files are nested inside the HFS partition.
            Parts = part.AsEnumerable();

            if (IsDirectory)
            {
                var folder = (HFSCatalogFolder)record;
                LastModifiedTime = (folder.AttributeModDate > folder.ContentModDate) ? folder.AttributeModDate : folder.ContentModDate;
                CreatedTime = folder.CreateDate;
                LastAccessedTime = folder.AccessDate;
                ArchivedTime = folder.BackupDate;
            }
            else
            {
                var file = (HFSCatalogFile)record;
                LastModifiedTime = (file.AttributeModDate > file.ContentModDate) ? file.AttributeModDate : file.ContentModDate;
                CreatedTime = file.CreateDate;
                LastAccessedTime = file.AccessDate;
                ArchivedTime = file.BackupDate;
            }
        }
    }
}
