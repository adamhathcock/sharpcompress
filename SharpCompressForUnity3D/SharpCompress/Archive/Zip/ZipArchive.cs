namespace SharpCompress.Archive.Zip
{
    using SharpCompress;
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.Reader;
    using SharpCompress.Reader.Zip;
    using SharpCompress.Writer.Zip;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    public class ZipArchive : AbstractWritableArchive<ZipArchiveEntry, ZipVolume>
    {
        [CompilerGenerated]
        private CompressionLevel _DeflateCompressionLevel_k__BackingField;
        private readonly SeekableZipHeaderFactory headerFactory;

        internal ZipArchive() : base(ArchiveType.Zip)
        {
        }

        internal ZipArchive(Stream stream, Options options, [Optional, DefaultParameterValue(null)] string password) : base(ArchiveType.Zip, stream, options)
        {
            this.headerFactory = new SeekableZipHeaderFactory(password);
        }

        public static ZipArchive Create()
        {
            return new ZipArchive();
        }

        protected override ZipArchiveEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified, bool closeStream)
        {
            return new ZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            Stream stream = Enumerable.Single<ZipVolume>(base.Volumes).Stream;
            stream.Position = 0L;
            return ZipReader.Open(stream, null, Options.KeepStreamsOpen);
        }

        public static bool IsZipFile(Stream stream, [Optional, DefaultParameterValue(null)] string password)
        {
            StreamingZipHeaderFactory factory = new StreamingZipHeaderFactory(password);
            try
            {
                ZipHeader header = Enumerable.FirstOrDefault<ZipHeader>(factory.ReadStreamHeader(stream), delegate (ZipHeader x) {
                    return x.ZipHeaderType != ZipHeaderType.Split;
                });
                if (header == null)
                {
                    return false;
                }
                return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
            }
            catch (CryptographicException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override IEnumerable<ZipArchiveEntry> LoadEntries(IEnumerable<ZipVolume> volumes)
        {
            ZipVolume iteratorVariable0 = Enumerable.Single<ZipVolume>(volumes);
            Stream stream = iteratorVariable0.Stream;
            foreach (ZipHeader iteratorVariable2 in this.headerFactory.ReadSeekableHeader(stream))
            {
                if (iteratorVariable2 != null)
                {
                    switch (iteratorVariable2.ZipHeaderType)
                    {
                        case ZipHeaderType.DirectoryEntry:
                        {
                            yield return new ZipArchiveEntry(this, new SeekableZipFilePart(this.headerFactory, iteratorVariable2 as DirectoryEntryHeader, stream));
                            continue;
                        }
                        case ZipHeaderType.DirectoryEnd:
                        {
                            byte[] bytes = (iteratorVariable2 as DirectoryEndHeader).Comment;
                            iteratorVariable0.Comment = ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
                            goto Label_015C;
                        }
                    }
                }
            }
        Label_015C:
            yield break;
        }

        protected override IEnumerable<ZipVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return Utility.AsEnumerable<ZipVolume>(new ZipVolume(Enumerable.First<Stream>(streams), options));
        }

        public static ZipArchive Open(Stream stream, [Optional, DefaultParameterValue(null)] string password)
        {
            Utility.CheckNotNull(stream, "stream");
            return Open(stream, Options.None, password);
        }

        public static ZipArchive Open(Stream stream, Options options, [Optional, DefaultParameterValue(null)] string password)
        {
            Utility.CheckNotNull(stream, "stream");
            return new ZipArchive(stream, options, password);
        }

        protected override void SaveTo(Stream stream, CompressionInfo compressionInfo, IEnumerable<ZipArchiveEntry> oldEntries, IEnumerable<ZipArchiveEntry> newEntries)
        {
            using (ZipWriter writer = new ZipWriter(stream, compressionInfo, string.Empty))
            {
                foreach (ZipArchiveEntry entry in Enumerable.Where<ZipArchiveEntry>(Enumerable.Concat<ZipArchiveEntry>(oldEntries, newEntries), delegate (ZipArchiveEntry x) {
                    return !x.IsDirectory;
                }))
                {
                    using (Stream stream2 = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, stream2, entry.LastModifiedTime, string.Empty, null);
                    }
                }
            }
        }

        public CompressionLevel DeflateCompressionLevel
        {
            [CompilerGenerated]
            get
            {
                return this._DeflateCompressionLevel_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._DeflateCompressionLevel_k__BackingField = value;
            }
        }

    }
}

