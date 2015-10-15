namespace SharpCompress.Archive.SevenZip
{
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.SevenZip;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class SevenZipArchiveEntry : SevenZipEntry, IArchiveEntry, IEntry
    {
        [CompilerGenerated]
        private IArchive _Archive_k__BackingField;

        internal SevenZipArchiveEntry(SevenZipArchive archive, SevenZipFilePart part) : base(part)
        {
            this.Archive = archive;
        }

        public Stream OpenEntryStream()
        {
            return base.FilePart.GetCompressedStream();
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

        public bool IsAnti
        {
            get
            {
                return base.FilePart.Header.IsAnti;
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

