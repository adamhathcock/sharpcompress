namespace SharpCompress.Common.Zip
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using System;
    using System.Collections.Generic;

    public class ZipEntry : Entry
    {
        private readonly ZipFilePart filePart;
        private readonly DateTime? lastModifiedTime;

        internal ZipEntry(ZipFilePart filePart)
        {
            if (filePart != null)
            {
                this.filePart = filePart;
                this.lastModifiedTime = new DateTime?(Utility.DosDateToDateTime(filePart.Header.LastModifiedDate, filePart.Header.LastModifiedTime));
            }
        }

        public override DateTime? ArchivedTime
        {
            get
            {
                return null;
            }
        }

        public override long CompressedSize
        {
            get
            {
                return (long) this.filePart.Header.CompressedSize;
            }
        }

        public override SharpCompress.Common.CompressionType CompressionType
        {
            get
            {
                switch (this.filePart.Header.CompressionMethod)
                {
                    case ZipCompressionMethod.BZip2:
                        return SharpCompress.Common.CompressionType.BZip2;

                    case ZipCompressionMethod.LZMA:
                        return SharpCompress.Common.CompressionType.LZMA;

                    case ZipCompressionMethod.PPMd:
                        return SharpCompress.Common.CompressionType.PPMd;

                    case ZipCompressionMethod.None:
                        return SharpCompress.Common.CompressionType.None;

                    case ZipCompressionMethod.Deflate:
                        return SharpCompress.Common.CompressionType.Deflate;
                }
                return SharpCompress.Common.CompressionType.Unknown;
            }
        }

        public override long Crc
        {
            get
            {
                return (long) this.filePart.Header.Crc;
            }
        }

        public override DateTime? CreatedTime
        {
            get
            {
                return null;
            }
        }

        public override bool IsDirectory
        {
            get
            {
                return this.filePart.Header.IsDirectory;
            }
        }

        public override bool IsEncrypted
        {
            get
            {
                return FlagUtility.HasFlag<HeaderFlags>(this.filePart.Header.Flags, HeaderFlags.Encrypted);
            }
        }

        public override bool IsSplit
        {
            get
            {
                return false;
            }
        }

        public override string Key
        {
            get
            {
                return this.filePart.Header.Name;
            }
        }

        public override DateTime? LastAccessedTime
        {
            get
            {
                return null;
            }
        }

        public override DateTime? LastModifiedTime
        {
            get
            {
                return this.lastModifiedTime;
            }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get
            {
                return Utility.AsEnumerable<FilePart>(this.filePart);
            }
        }

        public override long Size
        {
            get
            {
                return (long) this.filePart.Header.UncompressedSize;
            }
        }
    }
}

