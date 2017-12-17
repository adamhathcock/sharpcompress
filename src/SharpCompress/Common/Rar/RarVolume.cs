using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar
{
    /// <summary>
    /// A RarArchiveVolume is a single rar file that may or may not be a split RarArchive.  A Rar Archive is one to many Rar Parts
    /// </summary>
    public abstract class RarVolume : Volume
    {
        private readonly RarHeaderFactory headerFactory;

        internal RarVolume(StreamingMode mode, Stream stream, ReaderOptions options)
            : base(stream, options)
        {
            headerFactory = new RarHeaderFactory(mode, options);
        }

        internal StreamingMode Mode => headerFactory.StreamingMode;

        internal abstract IEnumerable<RarFilePart> ReadFileParts();

        internal abstract RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader);

        internal IEnumerable<RarFilePart> GetVolumeFileParts()
        {
            MarkHeader previousMarkHeader = null;
            foreach (var header in headerFactory.ReadHeaders(Stream))
            {
                switch (header.HeaderType)
                {
                    case HeaderType.Archive:
                    {
                        ArchiveHeader = header as ArchiveHeader;
                    }
                        break;
                    case HeaderType.Mark:
                    {
                        previousMarkHeader = header as MarkHeader;
                    }
                        break;
                    case HeaderType.File:
                    {
                        FileHeader fh = header as FileHeader;
                        RarFilePart fp = CreateFilePart(fh, previousMarkHeader);
                        yield return fp;
                    }
                        break;
                }
            }
        }

        internal ArchiveHeader ArchiveHeader { get; private set; }

        private void EnsureArchiveHeaderLoaded()
        {
            if (ArchiveHeader == null)
            {
                if (Mode == StreamingMode.Streaming)
                {
                    throw new InvalidOperationException("ArchiveHeader should never been null in a streaming read.");
                }

                //we only want to load the archive header to avoid overhead but have to do the nasty thing and reset the stream
                GetVolumeFileParts().First();
                Stream.Position = 0;
            }
        }

        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.
        /// Only Rar 3.0 format and higher
        /// </summary>
        public override bool IsFirstVolume
        {
            get
            {
                EnsureArchiveHeaderLoaded();
                return ArchiveHeader.IsFirstVolume;
            }
        }

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public override bool IsMultiVolume
        {
            get
            {
                EnsureArchiveHeaderLoaded();
                return ArchiveHeader.IsVolume;
            }
        }

        /// <summary>
        /// RarArchive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
        /// Currently, SharpCompress cannot decompress SOLID archives.
        /// </summary>
        public bool IsSolidArchive
        {
            get
            {
                EnsureArchiveHeaderLoaded();
                return ArchiveHeader.IsSolid;
            }
        }
    }
}