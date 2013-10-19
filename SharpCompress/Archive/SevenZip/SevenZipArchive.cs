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
#if !PORTABLE && !NETFX_CORE
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

#if !PORTABLE && !NETFX_CORE
        internal SevenZipArchive(FileInfo fileInfo, Options options)
            : base(ArchiveType.SevenZip, fileInfo, options)
        {
        }

        protected override IEnumerable<SevenZipVolume> LoadVolumes(FileInfo file, Options options)
        {
            return new SevenZipVolume(file, options).AsEnumerable();
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
            : base(ArchiveType.SevenZip, stream.AsEnumerable(), options)
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
                stream.Seek(FindSignature(stream),SeekOrigin.Begin);
                var reader = new ArchiveReader();
                reader.Open(stream);
                database = reader.ReadDatabase(null);
            }
        }


        public static bool IsSevenZipFile(Stream stream)
        {
            try
            {
                return FindSignature(stream)>-1;
            }
            catch
            {
                return false;
            }
        }

        private static readonly byte[] SIGNATURE = {(byte) '7', (byte) 'z', 0xBC, 0xAF, 0x27, 0x1C};
        const int MAX_BYTES_TO_ARCHIVE = 0x40000;
        public static long FindSignature(Stream stream){
            BinaryReader reader = new BinaryReader(stream);
            int j = 0;
            long match=-1;
            var maxPos = Math.Min(MAX_BYTES_TO_ARCHIVE+SIGNATURE.Length, stream.Length);
            for (; stream.Position < maxPos; ) {
                var bt = reader.ReadByte();
                if (bt == SIGNATURE[j]){
                    if (j == SIGNATURE.Length-1){
                        match = stream.Position - j-1;
                        break;
                    }
                    j++;
                    continue;
                }
                
                j = 0;
            }
            return match;
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            return new SevenZipReader(this);
        }

        public override bool IsSolid
        {
            get { return Entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder).Count() > 1; }
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
                return new EntryStream(new ReadOnlySubStream(currentStream, currentItem.Size));
            }
        }
    }
}