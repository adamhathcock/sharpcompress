using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Xz
{
    public class XzEntry : Entry
    {
        private readonly XzFilePart filePart;

        internal XzEntry(XzFilePart filePart)
        {
            this.filePart = filePart;
        }

        public override CompressionType CompressionType => CompressionType.GZip;

        public override long Crc => 0;

        public override string Key => filePart.FilePartName;

        public override long CompressedSize => 0;

        public override long Size => 0;

        public override DateTime? LastModifiedTime => filePart.DateModified;

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => false;

        public override bool IsSplit => false;

        internal override IEnumerable<FilePart> Parts => filePart.AsEnumerable<FilePart>();

        internal static IEnumerable<XzEntry> GetEntries(Stream stream)
        {
            yield return new XzEntry(new XzFilePart(stream));
        }
    }
}