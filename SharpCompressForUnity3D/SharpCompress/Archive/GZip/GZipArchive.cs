namespace SharpCompress.Archive.GZip
{
    using SharpCompress;
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.GZip;
    using SharpCompress.Reader;
    using SharpCompress.Reader.GZip;
    using SharpCompress.Writer.GZip;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
    {
        internal GZipArchive() : base(ArchiveType.GZip)
        {
        }

        internal GZipArchive(Stream stream, Options options) : base(ArchiveType.GZip, stream, options)
        {
        }

        public static GZipArchive Create()
        {
            return new GZipArchive();
        }

        protected override GZipArchiveEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified, bool closeStream)
        {
            if (Enumerable.Any<GZipArchiveEntry>(this.Entries))
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            return new GZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            Stream stream = Enumerable.Single<GZipVolume>(base.Volumes).Stream;
            stream.Position = 0L;
            return GZipReader.Open(stream, Options.KeepStreamsOpen);
        }

        public static bool IsGZipFile(Stream stream)
        {
            byte[] buffer = new byte[10];
            int num = stream.Read(buffer, 0, buffer.Length);
            if (num == 0)
            {
                return false;
            }
            if (num != 10)
            {
                return false;
            }
            if (((buffer[0] != 0x1f) || (buffer[1] != 0x8b)) || (buffer[2] != 8))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<GZipArchiveEntry> LoadEntries(IEnumerable<GZipVolume> volumes)
        {
            Stream stream = Enumerable.Single<GZipVolume>(volumes).Stream;
            yield return new GZipArchiveEntry(this, new GZipFilePart(stream));
        }

        protected override IEnumerable<GZipVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return Utility.AsEnumerable<GZipVolume>(new GZipVolume(Enumerable.First<Stream>(streams), options));
        }

        public static GZipArchive Open(Stream stream)
        {
            Utility.CheckNotNull(stream, "stream");
            return Open(stream, Options.None);
        }

        public static GZipArchive Open(Stream stream, Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new GZipArchive(stream, options);
        }

        public void SaveTo(Stream stream)
        {
            SaveTo(stream, CompressionType.Deflate );
        }

        protected override void SaveTo(Stream stream, CompressionInfo compressionInfo, IEnumerable<GZipArchiveEntry> oldEntries, IEnumerable<GZipArchiveEntry> newEntries)
        {
            if (this.Entries.Count > 1)
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            using (GZipWriter writer = new GZipWriter(stream))
            {
                foreach (GZipArchiveEntry entry in Enumerable.Where<GZipArchiveEntry>(Enumerable.Concat<GZipArchiveEntry>(oldEntries, newEntries), delegate (GZipArchiveEntry x) {
                    return !x.IsDirectory;
                }))
                {
                    using (Stream stream2 = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, stream2, entry.LastModifiedTime);
                    }
                }
            }
        }

    }
}

