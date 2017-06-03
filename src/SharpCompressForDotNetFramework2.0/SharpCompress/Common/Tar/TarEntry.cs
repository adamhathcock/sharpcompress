using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar
{
    public class TarEntry : Entry
    {
        private readonly TarFilePart filePart;
        private readonly CompressionType type;

        internal TarEntry(TarFilePart filePart, CompressionType type)
        {
            this.filePart = filePart;
            this.type = type;
        }

        public override CompressionType CompressionType
        {
            get { return type; }
        }

        public override uint Crc
        {
            get { return 0; }
        }

        public override string FilePath
        {
            get { return filePart.Header.Name; }
        }

        public override long CompressedSize
        {
            get { return filePart.Header.Size; }
        }

        public override long Size
        {
            get { return filePart.Header.Size; }
        }

        public override DateTime? LastModifiedTime
        {
            get { return filePart.Header.LastModifiedTime; }
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
            get { return filePart.Header.EntryType == EntryType.Directory; }
        }

        public override bool IsSplit
        {
            get { return false; }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { return filePart.AsEnumerable<FilePart>(); }
        }

        internal static IEnumerable<TarEntry> GetEntries(StreamingMode mode, Stream stream, CompressionType compressionType)
        {
            foreach (TarHeader h in TarHeaderFactory.ReadHeader(mode, stream))
            {
                if (h != null)
                {
                    if (mode == StreamingMode.Seekable)
                    {
                        yield return new TarEntry(new TarFilePart(h, stream), compressionType);
                    }
                    else
                    {
                        yield return new TarEntry(new TarFilePart(h, null), compressionType);
                    }
                }
            }
        }

        internal override void Close()
        {
        }
    }
}