using SharpCompress.Common.Dmg;
using SharpCompress.Common.Dmg.HFS;
using System;
using System.IO;

namespace SharpCompress.Archives.Dmg
{
    public sealed class DmgArchiveEntry : DmgEntry, IArchiveEntry
    {
        private readonly Stream? _stream;

        public bool IsComplete { get; } = true;

        public IArchive Archive { get; }

        internal DmgArchiveEntry(Stream? stream, DmgArchive archive, HFSCatalogRecord record, string path, DmgFilePart part)
            : base(record, path, stream?.Length ?? 0, part)
        {
            _stream = stream;
            Archive = archive;
        }

        public Stream OpenEntryStream()
        {
            if (IsDirectory)
                throw new NotSupportedException("Directories cannot be opened as stream");

            _stream!.Position = 0;
            return _stream;
        }
    }
}
