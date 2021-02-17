using SharpCompress.Common;
using SharpCompress.Common.Dmg;
using SharpCompress.Common.Dmg.Headers;
using SharpCompress.Common.Dmg.HFS;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpCompress.Archives.Dmg
{
    public class DmgArchive : AbstractArchive<DmgArchiveEntry, DmgVolume>
    {
        private readonly string _fileName;

        internal DmgArchive(FileInfo fileInfo, ReaderOptions readerOptions)
            : base(ArchiveType.Dmg, fileInfo, readerOptions)
        {
            _fileName = fileInfo.FullName;
        }

        internal DmgArchive(Stream stream, ReaderOptions readerOptions)
            : base(ArchiveType.Dmg, stream.AsEnumerable(), readerOptions)
        {
            _fileName = string.Empty;
        }

        protected override IReader CreateReaderForSolidExtraction()
            => new DmgReader(ReaderOptions, this, _fileName);

        protected override IEnumerable<DmgArchiveEntry> LoadEntries(IEnumerable<DmgVolume> volumes)
            => volumes.Single().LoadEntries();

        protected override IEnumerable<DmgVolume> LoadVolumes(FileInfo file)
            => new DmgVolume(this, file.OpenRead(), file.FullName, ReaderOptions).AsEnumerable();

        protected override IEnumerable<DmgVolume> LoadVolumes(IEnumerable<Stream> streams)
            => new DmgVolume(this, streams.Single(), string.Empty, ReaderOptions).AsEnumerable();

        public static bool IsDmgFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists) return false;

            using var stream = fileInfo.OpenRead();
            return IsDmgFile(stream);
        }

        public static bool IsDmgFile(Stream stream)
        {
            long headerPos = stream.Length - DmgHeader.HeaderSize;
            if (headerPos < 0) return false;
            stream.Position = headerPos;

            return DmgHeader.TryRead(stream, out _);
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="readerOptions"></param>
        public static DmgArchive Open(string filePath, ReaderOptions? readerOptions = null)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        public static DmgArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            fileInfo.CheckNotNull(nameof(fileInfo));
            return new DmgArchive(fileInfo, readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        public static DmgArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            stream.CheckNotNull(nameof(stream));
            return new DmgArchive(stream, readerOptions ?? new ReaderOptions());
        }

        private sealed class DmgReader : AbstractReader<DmgEntry, DmgVolume>
        {
            private readonly DmgArchive _archive;
            private readonly string _fileName;
            private readonly Stream? _partitionStream;

            public override DmgVolume Volume { get; }

            internal DmgReader(ReaderOptions readerOptions, DmgArchive archive, string fileName)
                : base(readerOptions, ArchiveType.Dmg)
            {
                _archive = archive;
                _fileName = fileName;
                Volume = archive.Volumes.Single();

                using var compressedStream = DmgUtil.LoadHFSPartitionStream(Volume.Stream, Volume.Header);
                _partitionStream = compressedStream?.Decompress();
            }

            protected override IEnumerable<DmgEntry> GetEntries(Stream stream)
            {
                if (_partitionStream is null) return Array.Empty<DmgArchiveEntry>();
                else return HFSUtil.LoadEntriesFromPartition(_partitionStream, _fileName, _archive);
            }
        }
    }
}
