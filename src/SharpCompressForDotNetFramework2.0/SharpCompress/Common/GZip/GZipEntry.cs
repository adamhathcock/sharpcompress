using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.GZip
{
    public class GZipEntry : Entry
    {
        private readonly GZipFilePart filePart;

        internal GZipEntry(GZipFilePart filePart)
        {
            this.filePart = filePart;
        }

        public override CompressionType CompressionType
        {
            get { return CompressionType.GZip; }
        }

        public override uint Crc
        {
            get { return 0; }
        }

        public override string FilePath
        {
            get { return filePart.FilePartName; }
        }

        public override long CompressedSize
        {
            get { return 0; }
        }

        public override long Size
        {
            get { return 0; }
        }

        public override DateTime? LastModifiedTime
        {
            get { return filePart.DateModified; }
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
            get { return false; }
        }

        public override bool IsSplit
        {
            get { return false; }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { return filePart.AsEnumerable<FilePart>(); }
        }

        internal static IEnumerable<GZipEntry> GetEntries(Stream stream)
        {
            yield return new GZipEntry(new GZipFilePart(stream));
        }

        internal override void Close()
        {
        }
    }
}