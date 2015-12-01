namespace SharpCompress.Archive.Zip
{
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public class ZipArchiveEntry : ZipEntry, IArchiveEntry, IEntry
    {
        [CompilerGenerated]
        private IArchive _Archive_k__BackingField;

        internal ZipArchiveEntry(ZipArchive archive, SeekableZipFilePart part) : base(part)
        {
            this.Archive = archive;
        }

        public virtual Stream OpenEntryStream()
        {
            return Enumerable.Single<FilePart>(this.Parts).GetCompressedStream();
        }

        public IArchive Archive
        {
            [CompilerGenerated]
            get
            {
                return this._Archive_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Archive_k__BackingField = value;
            }
        }

        public string Comment
        {
            get
            {
                return (Enumerable.Single<FilePart>(this.Parts) as SeekableZipFilePart).Comment;
            }
        }

        public bool IsComplete
        {
            get
            {
                return true;
            }
        }
    }
}

