using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Ace.Headers;

namespace SharpCompress.Common.Ace
{
    public class AceEntry : Entry
    {
        private readonly AceFilePart _filePart;

        internal AceEntry(AceFilePart filePart)
        {
            _filePart = filePart;
        }

        public override long Crc
        {
            get
            {
                if (_filePart == null)
                {
                    return 0;
                }
                return _filePart.Header.Crc32;
            }
        }

        public override string? Key => _filePart?.Header.Filename;

        public override string? LinkTarget => null;

        public override long CompressedSize => _filePart?.Header.PackedSize ?? 0;

        public override CompressionType CompressionType
        {
            get
            {
                if (_filePart.Header.CompressionType == Headers.CompressionType.Stored)
                {
                    return CompressionType.None;
                }
                return CompressionType.AceLZ77;
            }
        }

        public override long Size => _filePart?.Header.OriginalSize ?? 0;

        public override DateTime? LastModifiedTime => _filePart.Header.DateTime;

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => _filePart.Header.IsFileEncrypted;

        public override bool IsDirectory => _filePart.Header.IsDirectory;

        public override bool IsSplitAfter => false;

        internal override IEnumerable<FilePart> Parts => _filePart.Empty();
    }
}
