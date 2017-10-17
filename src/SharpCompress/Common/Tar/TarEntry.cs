using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using System.Text;

namespace SharpCompress.Common.Tar
{
    public class TarEntry : Entry
    {
        private readonly TarFilePart filePart;

        internal TarEntry(TarFilePart filePart, CompressionType type)
        {
            this.filePart = filePart;
            CompressionType = type;
        }

        public override CompressionType CompressionType { get; }

        public override long Crc => 0;

        public override string Key => filePart.Header.Name;

        public override long CompressedSize => filePart.Header.Size;

        public override long Size => filePart.Header.Size;

        public override DateTime? LastModifiedTime => filePart.Header.LastModifiedTime;

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => filePart.Header.EntryType == EntryType.Directory;

        public override bool IsSplit => false;

        internal override IEnumerable<FilePart> Parts => filePart.AsEnumerable<FilePart>();

        internal static IEnumerable<TarEntry> GetEntries(StreamingMode mode, Stream stream,
                                                         CompressionType compressionType, ArchiveEncoding archiveEncoding)
        {
            foreach (TarHeader h in TarHeaderFactory.ReadHeader(mode, stream, archiveEncoding))
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
    }
}