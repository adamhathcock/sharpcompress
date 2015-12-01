﻿namespace SharpCompress.Archive.Zip
{
    using SharpCompress.Archive;
    using SharpCompress.IO;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SharpCompress.Common;

    internal class ZipWritableArchiveEntry : ZipArchiveEntry, IWritableArchiveEntry
    {
        private readonly bool closeStream;
        private bool isDisposed;
        private readonly DateTime? lastModified;
        private readonly string path;
        private readonly long size;
        private readonly Stream stream;

        internal ZipWritableArchiveEntry(ZipArchive archive, Stream stream, string path, long size, DateTime? lastModified, bool closeStream) : base(archive, null)
        {
            this.stream = stream;
            this.path = path;
            this.size = size;
            this.lastModified = lastModified;
            this.closeStream = closeStream;
        }

        internal override void Close()
        {
            if (!(!this.closeStream || this.isDisposed))
            {
                this.stream.Dispose();
                this.isDisposed = true;
            }
        }

        public override Stream OpenEntryStream()
        {
            this.stream.Seek(0L, SeekOrigin.Begin);
            return new NonDisposingStream(this.stream);
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
                return this.path;
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
                return this.lastModified;
            }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        Stream IWritableArchiveEntry.Stream
        {
            get
            {
                return this.stream;
            }
        }

        public override long Size
        {
            get
            {
                return this.size;
            }
        }
    }
}

