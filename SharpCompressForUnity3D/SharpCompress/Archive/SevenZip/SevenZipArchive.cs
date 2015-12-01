namespace SharpCompress.Archive.SevenZip
{
    using SharpCompress;
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.SevenZip;
    using SharpCompress.IO;
    using SharpCompress.Reader;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class SevenZipArchive : AbstractArchive<SevenZipArchiveEntry, SevenZipVolume>
    {
        private ArchiveDatabase database;
        private static readonly byte[] SIGNATURE = new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c };

        internal SevenZipArchive() : base(ArchiveType.SevenZip)
        {
        }

        internal SevenZipArchive(Stream stream, Options options) : base(ArchiveType.SevenZip, Utility.AsEnumerable<Stream>(stream), options, null)
        {
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            return new SevenZipReader(this);
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

        protected override IEnumerable<SevenZipArchiveEntry> LoadEntries(IEnumerable<SevenZipVolume> volumes)
        {
            Stream stream = Enumerable.Single<SevenZipVolume>(volumes).Stream;
            this.LoadFactory(stream);
            for (int i = 0; i < this.database.Files.Count; i++)
            {
                CFileItem fileEntry = this.database.Files[i];
                if (!fileEntry.IsDir)
                {
                    yield return new SevenZipArchiveEntry(this, new SevenZipFilePart(stream, this.database, i, fileEntry));
                }
            }
        }

        private void LoadFactory(Stream stream)
        {
            if (this.database == null)
            {
                stream.Position = 0L;
                ArchiveReader reader = new ArchiveReader();
                reader.Open(stream);
                this.database = reader.ReadDatabase(null);
            }
        }

        protected override IEnumerable<SevenZipVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            foreach (Stream iteratorVariable0 in streams)
            {
                if (!(iteratorVariable0.CanRead && iteratorVariable0.CanSeek))
                {
                    throw new ArgumentException("Stream is not readable and seekable");
                }
                SevenZipVolume iteratorVariable1 = new SevenZipVolume(iteratorVariable0, options);
                yield return iteratorVariable1;
            }
        }

        public static SevenZipArchive Open(Stream stream)
        {
            Utility.CheckNotNull(stream, "stream");
            return Open(stream, Options.None);
        }

        public static SevenZipArchive Open(Stream stream, Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new SevenZipArchive(stream, options);
        }

        private static bool SignatureMatch(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            return Utility.BinaryEquals(reader.ReadBytes(6), SIGNATURE);
        }

        public override bool IsSolid
        {
            get
            {
                return (Enumerable.Count<IGrouping<CFolder, SevenZipArchiveEntry>>(Enumerable.GroupBy<SevenZipArchiveEntry, CFolder>(Enumerable.Where<SevenZipArchiveEntry>(this.Entries, delegate (SevenZipArchiveEntry x) {
                    return !x.IsDirectory;
                }), delegate (SevenZipArchiveEntry x) {
                    return x.FilePart.Folder;
                })) > 1);
            }
        }

        public override long TotalSize
        {
            get
            {
                int count = this.Entries.Count;
                return Enumerable.Aggregate<long, long>(this.database.PackSizes, 0L, delegate (long total, long packSize) {
                    return total + packSize;
                });
            }
        }



        private class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
        {
            private readonly SevenZipArchive archive;
            private CFolder currentFolder;
            private CFileItem currentItem;
            private Stream currentStream;

            internal SevenZipReader(SevenZipArchive archive) : base(Options.KeepStreamsOpen, ArchiveType.SevenZip)
            {
                this.archive = archive;
            }

            internal override IEnumerable<SevenZipEntry> GetEntries(Stream stream)
            {
                List<SevenZipArchiveEntry> source = Enumerable.ToList<SevenZipArchiveEntry>(this.archive.Entries);
                stream.Position = 0L;
                foreach (SevenZipArchiveEntry iteratorVariable1 in Enumerable.Where<SevenZipArchiveEntry>(source, delegate (SevenZipArchiveEntry x) {
                    return x.IsDirectory;
                }))
                {
                    yield return iteratorVariable1;
                }
                foreach (IGrouping<CFolder, SevenZipArchiveEntry> iteratorVariable2 in Enumerable.GroupBy<SevenZipArchiveEntry, CFolder>(Enumerable.Where<SevenZipArchiveEntry>(source, delegate (SevenZipArchiveEntry x) {
                    return !x.IsDirectory;
                }), delegate (SevenZipArchiveEntry x) {
                    return x.FilePart.Folder;
                }))
                {
                    this.currentFolder = iteratorVariable2.Key;
                    if (iteratorVariable2.Key == null)
                    {
                        this.currentStream = Stream.Null;
                    }
                    else
                    {
                        this.currentStream = this.archive.database.GetFolderStream(stream, this.currentFolder, null);
                    }
                    foreach (SevenZipArchiveEntry iteratorVariable3 in iteratorVariable2)
                    {
                        this.currentItem = iteratorVariable3.FilePart.Header;
                        yield return iteratorVariable3;
                    }
                }
            }

            protected override EntryStream GetEntryStream()
            {
                return base.CreateEntryStream(new ReadOnlySubStream(this.currentStream, this.currentItem.Size));
            }

            public override SevenZipVolume Volume
            {
                get
                {
                    return Enumerable.Single<SevenZipVolume>(this.archive.Volumes);
                }
            }

        }
    }
}

