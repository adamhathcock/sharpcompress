using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archive.Tar
{
    internal class TarWritableArchiveEntry : TarArchiveEntry
    {
        private string path;
        private long size;
        private DateTime? lastModified;
        private bool closeStream;

        internal TarWritableArchiveEntry(TarArchive archive, Stream stream, CompressionType compressionType,
            string path, long size, DateTime? lastModified, bool closeStream)
            : base(archive, null, compressionType)
        {
            this.Stream = stream;
            this.path = path;
            this.size = size;
            this.lastModified = lastModified;
            this.closeStream = closeStream;
        }

        public override uint Crc
        {
            get { return 0; }
        }

        public override string FilePath { get { return path; } }

        public override long CompressedSize
        {
            get { return 0; }
        }

        public override long Size { get { return size; } }

        public override DateTime? LastModifiedTime
        {
            get { return lastModified; }
        }

        public override DateTime? CreatedTime
        {
            get { return null; }
        }

        public override DateTime? LastAccessedTime
        {
            get { return null; }
        }

        public override DateTime? ArchivedTime
        {
            get { return null; }
        }

        public override bool IsEncrypted
        {
            get { return false; }
        }

        public override bool IsDirectory
        {
            get { return false; }
        }

        public override bool IsSplit
        {
            get { return false; }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { throw new NotImplementedException(); }
        }

        internal Stream Stream { get; private set; }

        public override Stream OpenEntryStream()
        {
            return new NonDisposingStream(Stream);
        }

        internal override void Close()
        {
           if (closeStream)
           {
              Stream.Dispose();
           }
        }
    }
}
