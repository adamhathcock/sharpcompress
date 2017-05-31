using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

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

        internal TarHeader Header => filePart.Header;

        public override CompressionType CompressionType { get; }

        public override long Crc => 0;

        public override string Key => filePart.Header.Name;

        public override long CompressedSize => filePart.Header.Size;

        public override long Size => filePart.Header.Size;

        public override DateTime? LastModifiedTime => filePart.Header.ModTime;

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => filePart.Header.TypeFlag == TarHeader.LF_DIR;

        public override bool IsSplit => false;

        internal override IEnumerable<FilePart> Parts => filePart.AsEnumerable<FilePart>();

        internal static IEnumerable<TarEntry> GetEntries(StreamingMode mode, Stream stream,
                                                         CompressionType compressionType)
        {
            using (var tarStream = new TarInputStream(stream))
            {
                TarHeader header = null;
                while ((header = tarStream.GetNextEntry()) != null)
                {
                    if (mode == StreamingMode.Seekable)
                    {
                        yield return new TarEntry(new TarFilePart(header, stream), compressionType);
                    }
                    else
                    {
                        yield return new TarEntry(new TarFilePart(header, null), compressionType);
                    }
                }
            }
        }
    }
}