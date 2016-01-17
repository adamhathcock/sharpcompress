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
        private readonly byte[] skipBuffer = new byte[4096];

        private void Skip(RarArchiveEntry en)
        {
            using (var s = new RarStream(archive.Unpack, en.FileHeader, new MultiVolumeReadOnlyStream(en.parts, archive)))
            {
                while (s.Read(skipBuffer, 0, skipBuffer.Length) > 0);
            }
        }

        private void MoveNext()
        {
            archive.SolidReadedEntries.Add(archive.SolidEntryEnumerator.Current);
            if (!archive.SolidEntryEnumerator.MoveNext())
            {
                archive.SolidEntryEnumerator = archive.Entries.GetEnumerator();
                archive.SolidReadedEntries = new List<RarArchiveEntry>();
                archive.SolidEntryEnumerator.MoveNext();
            }
        }
        public Stream OpenEntryStream()
        {
            if (archive.IsSolid)
            {
               
                if ((archive.SolidEntryEnumerator == null) || (archive.SolidReadedEntries.Any(b=>b.FileHeader==FileHeader)))
                {
                    archive.SolidEntryEnumerator = archive.Entries.GetEnumerator();
                    archive.SolidReadedEntries = new List<RarArchiveEntry>();
                    archive.SolidEntryEnumerator.MoveNext();
                }
                while (archive.SolidEntryEnumerator.Current.FileHeader != FileHeader)
                {
                    Skip(archive.SolidEntryEnumerator.Current);
                    MoveNext();
                }
                MoveNext();
            }
            return new RarStream(archive.Unpack, FileHeader, new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive));
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