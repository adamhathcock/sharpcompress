using System;
using System.Collections.Generic;

namespace SharpCompress.Common.SevenZip
{
    public class SevenZipEntry : Entry
    {
        internal SevenZipEntry(SevenZipFilePart filePart)
        {
            this.FilePart = filePart;
        }

        internal SevenZipFilePart FilePart { get; private set; }

        public override CompressionType CompressionType
        {
            get { return FilePart.CompressionType; }
        }

        public override long Crc
        {
            get { return FilePart.Header.Crc ?? 0; }
        }

        public override string Key
        {
            get { return FilePart.Header.Name; }
        }

        public override long CompressedSize
        {
            get { return 0; }
        }

        public override long Size
        {
            get { return (long) FilePart.Header.Size; }
        }

        public override DateTime? LastModifiedTime
        {
            get { return FilePart.Header.MTime; }
        }

        public override DateTime? CreatedTime
        {
            get { return null; }
        }

        public override DateTime? LastAccessedTime
        {
            get { return null; }
        }

        public override DateTime? ArchivedTime
        {
            get { return null; }
        }

        public override bool IsEncrypted
        {
            get { return false; }
        }

        public override bool IsDirectory
        {
            get { return FilePart.Header.IsDir; }
        }

        public override bool IsSplit
        {
            get { return false; }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { return FilePart.AsEnumerable<FilePart>(); }
        }
    }
}