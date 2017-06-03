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
            get
            {
                return FilePart.CompressionType;
            }
        }

        public override uint Crc
        {
            get { return (uint)FilePart.Header.FileCRC; }
        }

        public override string FilePath
        {
            get { return FilePart.Header.Name; }
        }

        public override long CompressedSize
        {
            get { return 0; }
        }

        public override long Size
        {
            get { return (long)FilePart.Header.Size; }
        }

        public override DateTime? LastModifiedTime
        {
            get { throw new NotImplementedException(); }
        }

        public override DateTime? CreatedTime
        {
            get { throw new NotImplementedException(); }
        }

        public override DateTime? LastAccessedTime
        {
            get { throw new NotImplementedException(); }
        }

        public override DateTime? ArchivedTime
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsEncrypted
        {
            get { return false; }
        }

        public override bool IsDirectory
        {
            get { return FilePart.Header.IsDirectory; }
        }

        public override bool IsSplit
        {
            get { return false; }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { return FilePart.AsEnumerable<FilePart>(); }
        }

        internal override void Close()
        {
        }
    }
}
