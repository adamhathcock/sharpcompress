using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Arc;
using SharpCompress.Common.Arj.Headers;

namespace SharpCompress.Common.Arj
{
    public class ArjEntry : Entry
    {
        private readonly ArjFilePart _filePart;

        internal ArjEntry(ArjFilePart filePart)
        {
            _filePart = filePart;
        }

        public override long Crc => _filePart.Header.OriginalCrc32;

        public override string? Key => _filePart?.Header.Name;

        public override string? LinkTarget => null;

        public override long CompressedSize => _filePart?.Header.CompressedSize ?? 0;

        public override CompressionType CompressionType
        {
            get
            {
                if (_filePart.Header.CompressionMethod == CompressionMethod.Stored)
                {
                    return CompressionType.None;
                }
                return CompressionType.ArjLZ77;
            }
        }

        public override long Size => _filePart?.Header.OriginalSize ?? 0;

        public override DateTime? LastModifiedTime => _filePart.Header.DateTimeModified.DateTime;

        public override DateTime? CreatedTime => _filePart.Header.DateTimeCreated.DateTime;

        public override DateTime? LastAccessedTime => _filePart.Header.DateTimeAccessed.DateTime;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => _filePart.Header.FileType == FileType.Directory;

        public override bool IsSplitAfter => false;

        internal override IEnumerable<FilePart> Parts => _filePart.Empty();
    }
}
