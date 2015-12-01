namespace SharpCompress.Archive.GZip
{
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.GZip;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public class GZipArchiveEntry : GZipEntry, IArchiveEntry, IEntry
    {
        [CompilerGenerated]
        private IArchive _Archive_k__BackingField;

        internal GZipArchiveEntry(GZipArchive archive, GZipFilePart part) : base(part)
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

        public bool IsComplete
        {
            get
            {
                return true;
            }
        }
    }
}

