namespace SharpCompress.Common.Tar
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.IO;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class TarEntry : Entry
    {
        private readonly TarFilePart filePart;
        private readonly SharpCompress.Common.CompressionType type;

        internal TarEntry(TarFilePart filePart, SharpCompress.Common.CompressionType type)
        {
            this.filePart = filePart;
            this.type = type;
        }

        internal static IEnumerable<TarEntry> GetEntries(StreamingMode mode, Stream stream, SharpCompress.Common.CompressionType compressionType)
        {
            foreach (TarHeader iteratorVariable0 in TarHeaderFactory.ReadHeader(mode, stream))
            {
                if (iteratorVariable0 == null)
                {
                    continue;
                }
                if (mode == StreamingMode.Seekable)
                {
                    yield return new TarEntry(new TarFilePart(iteratorVariable0, stream), compressionType);
                    continue;
                }
                yield return new TarEntry(new TarFilePart(iteratorVariable0, null), compressionType);
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
                return this.filePart.Header.Size;
            }
        }

        public override SharpCompress.Common.CompressionType CompressionType
        {
            get
            {
                return this.type;
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
                return (this.filePart.Header.EntryType == EntryType.Directory);
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
                return new DateTime?(this.filePart.Header.LastModifiedTime);
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
                return this.filePart.Header.Size;
            }
        }

    }
}

