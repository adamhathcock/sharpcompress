namespace SharpCompress.Common.GZip
{
    using SharpCompress;
    using SharpCompress.Common;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class GZipEntry : Entry
    {
        private readonly GZipFilePart filePart;

        internal GZipEntry(GZipFilePart filePart)
        {
            this.filePart = filePart;
        }

        internal static IEnumerable<GZipEntry> GetEntries(Stream stream)
        {
            yield return new GZipEntry(new GZipFilePart(stream));
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
                return 0L;
            }
        }

        public override SharpCompress.Common.CompressionType CompressionType
        {
            get
            {
                return SharpCompress.Common.CompressionType.GZip;
            }
        }

        public override long Crc
        {
            get
            {
                return 0L;
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
                return false;
            }
        }

        public override bool IsEncrypted
        {
            get
            {
                return false;
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
                return this.filePart.FilePartName;
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
                return this.filePart.DateModified;
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
                return 0L;
            }
        }

    }
}

