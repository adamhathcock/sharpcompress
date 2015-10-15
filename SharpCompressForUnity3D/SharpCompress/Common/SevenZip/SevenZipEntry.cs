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
        private SevenZipFilePart <FilePart>k__BackingField;

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
            get
            {
                return new int?(this.FilePart.Header.Attrib.Value);
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
                return this.FilePart.CompressionType;
            }
        }

        public override long Crc
        {
            get
            {
                uint? crc = this.FilePart.Header.Crc;
                return (crc.HasValue ? ((long) ((ulong) crc.GetValueOrDefault())) : ((long) ((ulong) 0)));
            }
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
                return this.<FilePart>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FilePart>k__BackingField = value;
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

