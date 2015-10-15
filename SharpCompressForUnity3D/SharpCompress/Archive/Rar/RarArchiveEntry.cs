namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using SharpCompress.Compressor.Rar;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    //public class RarArchiveEntry : RarEntry, IArchiveEntry, IEntry
    //{
    //    private readonly RarArchive archive;
    //    private readonly ICollection<RarFilePart> parts;

    //    internal RarArchiveEntry(RarArchive archive, IEnumerable<RarFilePart> parts)
    //    {
    //        this.parts = Enumerable.ToList<RarFilePart>(parts);
    //        this.archive = archive;
    //    }

    //    private void CheckIncomplete()
    //    {
    //        if (!this.IsComplete)
    //        {
    //            throw new IncompleteArchiveException("ArchiveEntry is incomplete and cannot perform this operation.");
    //        }
    //    }

    //    private bool fileFlags_HasFlag(FileFlags fileFlags1, FileFlags fileFlags2)
    //    {
    //        return (((ushort) (fileFlags1 & fileFlags2)) == fileFlags2);
    //    }

    //    public Stream OpenEntryStream()
    //    {
    //        return new RarStream(this.archive.Unpack, this.FileHeader, new MultiVolumeReadOnlyStream(Enumerable.Cast<RarFilePart>(this.Parts), this.archive));
    //    }

    //    public IArchive Archive
    //    {
    //        get
    //        {
    //            return this.archive;
    //        }
    //    }

    //    public override long CompressedSize
    //    {
    //        get
    //        {
    //            this.CheckIncomplete();
    //            return Enumerable.Aggregate<RarFilePart, long>(this.parts, 0L, delegate (long total, RarFilePart fp) {
    //                return total + fp.FileHeader.CompressedSize;
    //            });
    //        }
    //    }

    //    public override SharpCompress.Common.CompressionType CompressionType
    //    {
    //        get
    //        {
    //            return SharpCompress.Common.CompressionType.Rar;
    //        }
    //    }

    //    public override long Crc
    //    {
    //        get
    //        {
    //            this.CheckIncomplete();
    //            return (long) Enumerable.Single<SharpCompress.Common.Rar.Headers.FileHeader>(Enumerable.Select<RarFilePart, SharpCompress.Common.Rar.Headers.FileHeader>(this.parts, delegate (RarFilePart fp) {
    //                return fp.FileHeader;
    //            }), delegate (SharpCompress.Common.Rar.Headers.FileHeader fh) {
    //                return !this.fileFlags_HasFlag(fh.FileFlags, FileFlags.SPLIT_AFTER);
    //            }).FileCRC;
    //        }
    //    }

    //    internal override SharpCompress.Common.Rar.Headers.FileHeader FileHeader
    //    {
    //        get
    //        {
    //            return Enumerable.First<RarFilePart>(this.parts).FileHeader;
    //        }
    //    }

    //    public bool IsComplete
    //    {
    //        get
    //        {
    //            return Enumerable.Any<SharpCompress.Common.Rar.Headers.FileHeader>(Enumerable.Select<RarFilePart, SharpCompress.Common.Rar.Headers.FileHeader>(this.parts, delegate (RarFilePart fp) {
    //                return fp.FileHeader;
    //            }), delegate (SharpCompress.Common.Rar.Headers.FileHeader fh) {
    //                return !this.fileFlags_HasFlag(fh.FileFlags, FileFlags.SPLIT_AFTER);
    //            });
    //        }
    //    }

    //    internal override IEnumerable<FilePart> Parts
    //    {
    //        get
    //        {
    //            return Enumerable.Cast<FilePart>(this.parts);
    //        }
    //    }

    //    public override long Size
    //    {
    //        get
    //        {
    //            this.CheckIncomplete();
    //            return Enumerable.First<RarFilePart>(this.parts).FileHeader.UncompressedSize;
    //        }
    //    }
    //}

    public class RarArchiveEntry : RarEntry, IArchiveEntry {
        private readonly ICollection<RarFilePart> parts;
        private readonly RarArchive archive;

        internal RarArchiveEntry(RarArchive archive, IEnumerable<RarFilePart> parts) {
            this.parts = parts.ToList();
            this.archive = archive;
        }

        public override CompressionType CompressionType {
            get { return CompressionType.Rar; }
        }

        public IArchive Archive {
            get {
                return archive;
            }
        }

        internal override IEnumerable<FilePart> Parts {
            get { return parts.Cast<FilePart>(); }
        }

        internal override FileHeader FileHeader {
            get { return parts.First().FileHeader; }
        }

        public override long Crc {
            get {
                CheckIncomplete();
                return parts.Select(fp => fp.FileHeader)
                    .Single(fh => !fileFlags_HasFlag(fh.FileFlags, FileFlags.SPLIT_AFTER)).FileCRC;
            }
        }

        private bool fileFlags_HasFlag(FileFlags fileFlags1, FileFlags fileFlags2) {
            return (fileFlags1 & fileFlags2) == fileFlags2;
        }


        public override long Size {
            get {
                CheckIncomplete();
                return parts.First().FileHeader.UncompressedSize;
            }
        }

        public override long CompressedSize {
            get {
                CheckIncomplete();
                return parts.Aggregate(0L, (total, fp) => { return total + fp.FileHeader.CompressedSize; });
            }
        }

        public Stream OpenEntryStream() {
            return new RarStream(archive.Unpack, FileHeader,
                                 new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive));
        }

        public bool IsComplete {
            get { return parts.Select(fp => fp.FileHeader).Any(fh => !fileFlags_HasFlag(fh.FileFlags, FileFlags.SPLIT_AFTER)); }
        }

        private void CheckIncomplete() {
            if (!IsComplete) {
                throw new IncompleteArchiveException("ArchiveEntry is incomplete and cannot perform this operation.");
            }
        }
    }
}

