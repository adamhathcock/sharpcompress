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

        internal RarArchiveEntry(RarArchive archive, IEnumerable<RarFilePart> parts)
        {
            this.parts = parts.ToList();
            Archive = archive;
        }

        public override CompressionType CompressionType
        {
            get { return CompressionType.Rar; }
        }

        private RarArchive Archive
        {
            get;
            set;
        }

        internal override IEnumerable<FilePart> Parts
        {
            get
            {
                return parts.Cast<FilePart>();
            }
        }

        internal override FileHeader FileHeader
        {
            get
            {
                return parts.First().FileHeader;
            }
        }

        public override uint Crc
        {
            get
            {
                CheckIncomplete();
                return parts.Select(fp => fp.FileHeader)
                    .Where(fh => !fh.FileFlags.HasFlag(FileFlags.SPLIT_AFTER))
                    .Single().FileCRC;
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
                return parts.Aggregate(0L, (total, fp) =>
                {
                    return total + fp.FileHeader.CompressedSize;
                });
            }
        }

        public Stream OpenEntryStream()
        {
            return new RarStream(Archive.Unpack, FileHeader,
                                 new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), Archive));
        }

        public void WriteTo(Stream streamToWriteTo)
        {
            CheckIncomplete();
            if (Archive.IsSolidArchive())
            {
                throw new InvalidFormatException("Cannot use Archive random access on SOLID Rar files.");
            }
            if (IsEncrypted)
            {
                throw new PasswordProtectedException("Entry is password protected and cannot be extracted.");
            }
            this.Extract(Archive, streamToWriteTo);
        }

        public bool IsComplete
        {
            get
            {
                return parts.Select(fp => fp.FileHeader).Any(fh => !fh.FileFlags.HasFlag(FileFlags.SPLIT_AFTER));
            }
        }

        private void CheckIncomplete()
        {
            if (!IsComplete)
            {
                throw new IncompleteArchiveException("ArchiveEntry is incomplete and cannot perform this operation.");
            }
        }

        internal override void Close()
        {
        }
    }
}