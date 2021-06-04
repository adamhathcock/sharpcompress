using SharpCompress.Archives.Dmg;
using SharpCompress.Common.Dmg.Headers;
using SharpCompress.Common.Dmg.HFS;
using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Dmg
{
    public class DmgVolume : Volume
    {
        private readonly DmgArchive _archive;
        private readonly string _fileName;

        internal DmgHeader Header { get; }

        public DmgVolume(DmgArchive archive, Stream stream, string fileName, Readers.ReaderOptions readerOptions)
            : base(stream, readerOptions)
        {
            _archive = archive;
            _fileName = fileName;

            long pos = stream.Length - DmgHeader.HeaderSize;
            if (pos < 0) throw new InvalidFormatException("Invalid DMG volume");
            stream.Position = pos;

            if (DmgHeader.TryRead(stream, out var header)) Header = header!;
            else throw new InvalidFormatException("Invalid DMG volume");
        }

        internal IEnumerable<DmgArchiveEntry> LoadEntries()
        {
            var partitionStream = DmgUtil.LoadHFSPartitionStream(Stream, Header);
            if (partitionStream is null) return Array.Empty<DmgArchiveEntry>();
            else return HFSUtil.LoadEntriesFromPartition(partitionStream, _fileName, _archive);
        }
    }
}
