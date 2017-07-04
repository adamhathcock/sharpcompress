using System;
using System.Collections.Generic;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip
{
    public class ZipEntry : Entry
    {
        private readonly ZipFilePart filePart;

        internal ZipEntry(ZipFilePart filePart)
        {
            if (filePart != null)
            {
                this.filePart = filePart;
                LastModifiedTime = Utility.DosDateToDateTime(filePart.Header.LastModifiedDate,
                                                             filePart.Header.LastModifiedTime);
            }
        }

        public override CompressionType CompressionType
        {
            get
            {
                switch (filePart.Header.CompressionMethod)
                {
                    case ZipCompressionMethod.BZip2:
                    {
                        return CompressionType.BZip2;
                    }
                    case ZipCompressionMethod.Deflate:
                    {
                        return CompressionType.Deflate;
                    }
                    case ZipCompressionMethod.LZMA:
                    {
                        return CompressionType.LZMA;
                    }
                    case ZipCompressionMethod.PPMd:
                    {
                        return CompressionType.PPMd;
                    }
                    case ZipCompressionMethod.None:
                    {
                        return CompressionType.None;
                    }
                    default:
                    {
                        return CompressionType.Unknown;
                    }
                }
            }
        }

        public override long Crc => filePart.Header.Crc;

        public override string Key => filePart.Header.Name;

        public override long CompressedSize => filePart.Header.CompressedSize;

        public override long Size => filePart.Header.UncompressedSize;

        public override DateTime? LastModifiedTime { get; }

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => FlagUtility.HasFlag(filePart.Header.Flags, HeaderFlags.Encrypted);

        public override bool IsDirectory => filePart.Header.IsDirectory;

        public override bool IsSplit => false;

        internal override IEnumerable<FilePart> Parts => filePart.AsEnumerable<FilePart>();
    }
}