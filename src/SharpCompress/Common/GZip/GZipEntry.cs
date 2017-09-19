using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpCompress.Common.GZip
{
    public class GZipEntry : Entry
    {
        private readonly GZipFilePart filePart;

        internal GZipEntry(GZipFilePart filePart)
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

        internal static IEnumerable<GZipEntry> GetEntries(Stream stream, OptionsBase options)
        {
            yield return new GZipEntry(new GZipFilePart(stream, options.ArchiveEncoding));
        }
    }
}