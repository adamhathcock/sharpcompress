namespace SharpCompress.Archive.Tar
{
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Tar;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public class TarArchiveEntry : TarEntry, IArchiveEntry, IEntry
    {
        [CompilerGenerated]
        private IArchive <Archive>k__BackingField;

        internal TarArchiveEntry(TarArchive archive, TarFilePart part, CompressionType compressionType) : base(part, compressionType)
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
                return this.<Archive>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Archive>k__BackingField = value;
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

