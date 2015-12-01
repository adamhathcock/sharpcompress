namespace SharpCompress.Common.SevenZip
{
    using SharpCompress;
    using SharpCompress.Common;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class SevenZipEntry : Entry
    {
        [CompilerGenerated]
        private SevenZipFilePart _FilePart_k__BackingField;

        internal SevenZipEntry(SevenZipFilePart filePart)
        {
            this.FilePart = filePart;
        }

        public override DateTime? ArchivedTime
        {
            get
            {
                return null;
            }
        }

        public override int? Attrib
        {
            get { return (int)FilePart.Header.Attrib; }
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
                return this.FilePart.CompressionType;
            }
        }

        public override long Crc {
            get { return FilePart.Header.Crc ?? 0; }
        }

        public override DateTime? CreatedTime
        {
            get
            {
                return null;
            }
        }

        internal SevenZipFilePart FilePart
        {
            [CompilerGenerated]
            get
            {
                return this._FilePart_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._FilePart_k__BackingField = value;
            }
        }

        public override bool IsDirectory
        {
            get
            {
                return this.FilePart.Header.IsDir;
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
                return this.FilePart.Header.Name;
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
                return this.FilePart.Header.MTime;
            }
        }

        internal override IEnumerable<SharpCompress.Common.FilePart> Parts
        {
            get
            {
                return Utility.AsEnumerable<SharpCompress.Common.FilePart>(this.FilePart);
            }
        }

        public override long Size
        {
            get
            {
                return this.FilePart.Header.Size;
            }
        }
    }
}

