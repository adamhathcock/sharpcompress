namespace SharpCompress.Common.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using SharpCompress.IO;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public abstract class RarVolume : Volume
    {
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.ArchiveHeader <ArchiveHeader>k__BackingField;
        [CompilerGenerated]
        private string <Password>k__BackingField;
        private readonly RarHeaderFactory headerFactory;

        internal RarVolume(StreamingMode mode, Stream stream, string password, Options options) : base(stream, options)
        {
            this.headerFactory = new RarHeaderFactory(mode, options, password);
            this.Password = password;
        }

        private bool ArchiveHeader_HasFlag(ArchiveFlags ahf, ArchiveFlags archiveFlags)
        {
            return ((ahf & archiveFlags) == archiveFlags);
        }

        internal abstract RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader);
        private void EnsureArchiveHeaderLoaded()
        {
            if (this.ArchiveHeader == null)
            {
                if (this.Mode == StreamingMode.Streaming)
                {
                    throw new InvalidOperationException("ArchiveHeader should never been null in a streaming read.");
                }
                Enumerable.First<RarFilePart>(this.GetVolumeFileParts());
                base.Stream.Position = 0L;
            }
        }

        internal IEnumerable<RarFilePart> GetVolumeFileParts()
        {
            MarkHeader markHeader = null;
            foreach (RarHeader iteratorVariable1 in this.headerFactory.ReadHeaders(this.Stream))
            {
                switch (iteratorVariable1.HeaderType)
                {
                    case HeaderType.MarkHeader:
                    {
                        markHeader = iteratorVariable1 as MarkHeader;
                        continue;
                    }
                    case HeaderType.ArchiveHeader:
                    {
                        this.ArchiveHeader = iteratorVariable1 as SharpCompress.Common.Rar.Headers.ArchiveHeader;
                        continue;
                    }
                    case HeaderType.FileHeader:
                    {
                        FileHeader fileHeader = iteratorVariable1 as FileHeader;
                        RarFilePart iteratorVariable3 = this.CreateFilePart(fileHeader, markHeader);
                        yield return iteratorVariable3;
                        break;
                    }
                    default:
                    {
                        continue;
                    }
                }
            }
        }

        internal abstract IEnumerable<RarFilePart> ReadFileParts();

        internal SharpCompress.Common.Rar.Headers.ArchiveHeader ArchiveHeader
        {
            [CompilerGenerated]
            get
            {
                return this.<ArchiveHeader>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ArchiveHeader>k__BackingField = value;
            }
        }

        public override bool IsFirstVolume
        {
            get
            {
                this.EnsureArchiveHeaderLoaded();
                ArchiveFlags archiveHeaderFlags = this.ArchiveHeader.ArchiveHeaderFlags;
                return this.ArchiveHeader_HasFlag(archiveHeaderFlags, ArchiveFlags.FIRSTVOLUME);
            }
        }

        public override bool IsMultiVolume
        {
            get
            {
                this.EnsureArchiveHeaderLoaded();
                return this.ArchiveHeader_HasFlag(this.ArchiveHeader.ArchiveHeaderFlags, ArchiveFlags.VOLUME);
            }
        }

        public bool IsSolidArchive
        {
            get
            {
                this.EnsureArchiveHeaderLoaded();
                return this.ArchiveHeader_HasFlag(this.ArchiveHeader.ArchiveHeaderFlags, ArchiveFlags.SOLID);
            }
        }

        internal StreamingMode Mode
        {
            get
            {
                return this.headerFactory.StreamingMode;
            }
        }

        internal string Password
        {
            [CompilerGenerated]
            get
            {
                return this.<Password>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Password>k__BackingField = value;
            }
        }

    }
}

