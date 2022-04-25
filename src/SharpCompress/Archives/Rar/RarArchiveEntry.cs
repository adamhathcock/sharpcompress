using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar
{
    public class RarArchiveEntry : RarEntry, IArchiveEntry
    {
        private readonly ICollection<RarFilePart> parts;
        private readonly RarArchive archive;
        private readonly ReaderOptions readerOptions;

        internal RarArchiveEntry(RarArchive archive, IEnumerable<RarFilePart> parts, ReaderOptions readerOptions)
        {
            this.parts = parts.ToList();
            this.archive = archive;
            this.readerOptions = readerOptions;
            this.IsSolid = this.FileHeader.IsSolid;
        }

        public override CompressionType CompressionType => CompressionType.Rar;

        public IArchive Archive => archive;

        internal override IEnumerable<FilePart> Parts => parts.Cast<FilePart>();

        internal override FileHeader FileHeader => parts.First().FileHeader;

        public override long Crc
        {
            get
            {
                CheckIncomplete();
                return parts.Select(fp => fp.FileHeader).Single(fh => !fh.IsSplitAfter).FileCrc;
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
                return parts.Aggregate(0L, (total, fp) => total + fp.FileHeader.CompressedSize);
            }
        }

        public Stream OpenEntryStream()
        {
            if (IsRarV3)
            {
                return new RarStream(archive.UnpackV1.Value, FileHeader, new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive));
            }

            return new RarStream(archive.UnpackV2017.Value, FileHeader, new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive));
        }

        public bool IsComplete
        {
            get
            {
                var headers = parts.Select(x => x.FileHeader);
                return !headers.First().IsSplitBefore && !headers.Last().IsSplitAfter;
            }
        }

        private void CheckIncomplete()
        {
            if (!readerOptions.DisableCheckIncomplete && !IsComplete)
            {
                throw new IncompleteArchiveException("ArchiveEntry is incomplete and cannot perform this operation.");
            }
        }
    }
}