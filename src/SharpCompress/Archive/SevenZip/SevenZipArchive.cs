using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.IO;
using SharpCompress.Reader;

namespace SharpCompress.Archive.SevenZip
{
    public class SevenZipArchive : AbstractArchive<SevenZipArchiveEntry, SevenZipVolume>
    {
        private ArchiveDatabase database;
#if !NO_FILE
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        public static SevenZipArchive Open(string filePath)
        {
            return Open(filePath, Options.None);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        public static SevenZipArchive Open(FileInfo fileInfo)
        {
            return Open(fileInfo, Options.None);
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static SevenZipArchive Open(string filePath, Options options)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static SevenZipArchive Open(FileInfo fileInfo, Options options)
        {
            fileInfo.CheckNotNull("fileInfo");
            return new SevenZipArchive(fileInfo, options);
        }
#endif

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        public static SevenZipArchive Open(Stream stream)
        {
            stream.CheckNotNull("stream");
            return Open(stream, Options.None);
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        public static SevenZipArchive Open(Stream stream, Options options)
        {
            stream.CheckNotNull("stream");
            return new SevenZipArchive(stream, options);
        }

#if !NO_FILE
        internal SevenZipArchive(FileInfo fileInfo, Options options)
            : base(ArchiveType.SevenZip, fileInfo, options, null)
        {
        }

        protected override IEnumerable<SevenZipVolume> LoadVolumes(FileInfo file, Options options)
        {
            if (FlagUtility.HasFlag(options, Options.KeepStreamsOpen))
            {
                options = (Options)FlagUtility.SetFlag(options, Options.KeepStreamsOpen, false);
            }
            return new SevenZipVolume(file.OpenRead(), options).AsEnumerable();
        }

        public static bool IsSevenZipFile(string filePath)
        {
            return IsSevenZipFile(new FileInfo(filePath));
        }

        public static bool IsSevenZipFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }
            using (Stream stream = fileInfo.OpenRead())
            {
                return IsSevenZipFile(stream);
            }
        }
#endif

        internal SevenZipArchive(Stream stream, Options options)
            : base(ArchiveType.SevenZip, stream.AsEnumerable(), options, null)
        {
        }

        internal SevenZipArchive()
            : base(ArchiveType.SevenZip)
        {
        }

        protected override IEnumerable<SevenZipVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            foreach (Stream s in streams)
            {
                if (!s.CanRead || !s.CanSeek)
                {
                    throw new ArgumentException("Stream is not readable and seekable");
                }
                SevenZipVolume volume = new SevenZipVolume(s, options);
                yield return volume;
            }
        }

        protected override IEnumerable<SevenZipArchiveEntry> LoadEntries(IEnumerable<SevenZipVolume> volumes)
        {
            var stream = volumes.Single().Stream;
            LoadFactory(stream);
            for (int i = 0; i < database.Files.Count; i++)
            {
                var file = database.Files[i];
                if (!file.IsDir)
                {
                    yield return new SevenZipArchiveEntry(this, new SevenZipFilePart(stream, database, i, file));
                }
            }
        }

        private void LoadFactory(Stream stream)
        {
            if (database == null)
            {
                stream.Position = 0;
                var reader = new ArchiveReader();
                reader.Open(stream);
                database = reader.ReadDatabase(null);
            }
        }


        public static bool IsSevenZipFile(Stream stream)
        {
            try
            {
                return SignatureMatch(stream);
            }
            catch
            {
                return false;
            }
        }

        private static readonly byte[] SIGNATURE = new byte[] {(byte) '7', (byte) 'z', 0xBC, 0xAF, 0x27, 0x1C};

        private static bool SignatureMatch(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            byte[] signatureBytes = reader.ReadBytes(6);
            return signatureBytes.BinaryEquals(SIGNATURE);
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            return new SevenZipReader(this);
        }

        public override bool IsSolid
        {
            get { return Entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder).Count() > 1; }
        }

        public override long TotalSize
        {
            get
            {
                int i = Entries.Count;
                return database.PackSizes.Aggregate(0L, (total, packSize) => total + packSize);
            }
        }

        private class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
        {
            private readonly SevenZipArchive archive;
            private CFolder currentFolder;
            private Stream currentStream;
            private CFileItem currentItem;

            internal SevenZipReader(SevenZipArchive archive)
                : base(Options.KeepStreamsOpen, ArchiveType.SevenZip)
            {
                this.archive = archive;
            }


            public override SevenZipVolume Volume
            {
                get { return archive.Volumes.Single(); }
            }

            internal override IEnumerable<SevenZipEntry> GetEntries(Stream stream)
            {
                List<SevenZipArchiveEntry> entries = archive.Entries.ToList();
                stream.Position = 0;
                foreach (var dir in entries.Where(x => x.IsDirectory))
                {
                    yield return dir;
                }
                foreach (var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder))
                {
                    currentFolder = group.Key;
                    if (group.Key == null)
                    {
                        currentStream = Stream.Null;
                    }
                    else
                    {
                        currentStream = archive.database.GetFolderStream(stream, currentFolder, null);
                    }
                    foreach (var entry in group)
                    {
                        currentItem = entry.FilePart.Header;
                        yield return entry;
                    }
                }
            }

            protected override EntryStream GetEntryStream()
            {
                return CreateEntryStream(new ReadOnlySubStream(currentStream, currentItem.Size));
            }
        }
    }
}