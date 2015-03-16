using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressor.Rar;

namespace SharpCompress.Archive.Rar
{
    public class RarArchiveEntry : RarEntry, IArchiveEntry
    {
        private readonly ICollection<RarFilePart> parts;
        private readonly RarArchive archive;

        internal RarArchiveEntry(RarArchive archive, IEnumerable<RarFilePart> parts)
        {
            this.parts = parts.ToList();
            this.archive = archive;
        }

        public override CompressionType CompressionType
        {
            get { return CompressionType.Rar; }
        }

        public IArchive Archive
        {
            get
            {
                return archive;
            }
        }

        internal override IEnumerable<FilePart> Parts
        {
            get { return parts.Cast<FilePart>(); }
        }

        internal override FileHeader FileHeader
        {
            get { return parts.First().FileHeader; }
        }

        public override long Crc
        {
            get
            {
                CheckIncomplete();
                return parts.Select(fp => fp.FileHeader)
                    .Single(fh => !fh.FileFlags.HasFlag(FileFlags.SPLIT_AFTER)).FileCRC;
            }
        }


        public override long Size
        {
            get
            {
                CheckIncomplete();
                return parts.First().FileHeader.UncompressedSize;
            }
        }

        public override long CompressedSize
        {
            get
            {
                CheckIncomplete();
                return parts.Aggregate(0L, (total, fp) => { return total + fp.FileHeader.CompressedSize; });
            }
        }

        public Stream OpenEntryStream()
        {
            return new RarStream(archive.Unpack, FileHeader,
                                 new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive));
        }

        public bool IsComplete
        {
            get { return parts.Select(fp => fp.FileHeader).Any(fh => !fh.FileFlags.HasFlag(FileFlags.SPLIT_AFTER)); }
        }

        private void CheckIncomplete()
        {
            if (!IsComplete)
            {
                throw new IncompleteArchiveException("ArchiveEntry is incomplete and cannot perform this operation.");
            }
        }
    }
}