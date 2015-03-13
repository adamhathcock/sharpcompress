using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Reader;
using SharpCompress.Reader.GZip;
using SharpCompress.Writer.GZip;

namespace SharpCompress.Archive.GZip
{
    public class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
    {
#if !PORTABLE && !NETFX_CORE
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        public static GZipArchive Open(string filePath)
        {
            return Open(filePath, Options.None);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        public static GZipArchive Open(FileInfo fileInfo)
        {
            return Open(fileInfo, Options.None);
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static GZipArchive Open(string filePath, Options options)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static GZipArchive Open(FileInfo fileInfo, Options options)
        {
            fileInfo.CheckNotNull("fileInfo");
            return new GZipArchive(fileInfo, options);
        }
#endif

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        public static GZipArchive Open(Stream stream)
        {
            stream.CheckNotNull("stream");
            return Open(stream, Options.None);
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        public static GZipArchive Open(Stream stream, Options options)
        {
            stream.CheckNotNull("stream");
            return new GZipArchive(stream, options);
        }

        public static GZipArchive Create()
        {
            return new GZipArchive();
        }

#if !PORTABLE && !NETFX_CORE
        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        internal GZipArchive(FileInfo fileInfo, Options options)
            : base(ArchiveType.GZip, fileInfo, options)
        {
        }

        protected override IEnumerable<GZipVolume> LoadVolumes(FileInfo file, Options options)
        {
            return new GZipVolume(file, options).AsEnumerable();
        }

        public static bool IsGZipFile(string filePath)
        {
            return IsGZipFile(new FileInfo(filePath));
        }

        public static bool IsGZipFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }
            using (Stream stream = fileInfo.OpenRead())
            {
                return IsGZipFile(stream);
            }
        }

        public void SaveTo(string filePath)
        {
            SaveTo(new FileInfo(filePath));
        }

        public void SaveTo(FileInfo fileInfo)
        {
            using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                SaveTo(stream);
            }
        }
#endif

        public static bool IsGZipFile(Stream stream)
        {
            // read the header on the first read
            byte[] header = new byte[10];
            int n = stream.Read(header, 0, header.Length);

            // workitem 8501: handle edge case (decompress empty stream)
            if (n == 0)
                return false;

            if (n != 10)
                return false;

            if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
                return false;

            return true;
        }

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        internal GZipArchive(Stream stream, Options options)
            : base(ArchiveType.GZip, stream, options)
        {
        }

        internal GZipArchive()
            : base(ArchiveType.GZip)
        {
        }

        public void SaveTo(Stream stream)
        {
            SaveTo(stream, CompressionType.GZip);
        }

        protected override GZipArchiveEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified,
                                                        bool closeStream)
        {
            if (Entries.Any())
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            return new GZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
        }

        protected override void SaveTo(Stream stream, CompressionInfo compressionInfo,
                                       IEnumerable<GZipArchiveEntry> oldEntries,
                                       IEnumerable<GZipArchiveEntry> newEntries)
        {
            if (Entries.Count > 1)
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            using (var writer = new GZipWriter(stream))
            {
                foreach (var entry in oldEntries.Concat(newEntries)
                                                .Where(x => !x.IsDirectory))
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, entryStream, entry.LastModifiedTime);
                    }
                }
            }
        }

        protected override IEnumerable<GZipVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return new GZipVolume(streams.First(), options).AsEnumerable();
        }

        protected override IEnumerable<GZipArchiveEntry> LoadEntries(IEnumerable<GZipVolume> volumes)
        {
            Stream stream = volumes.Single().Stream;
            yield return new GZipArchiveEntry(this, new GZipFilePart(stream));
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            var stream = Volumes.Single().Stream;
            stream.Position = 0;
            return GZipReader.Open(stream);
        }
    }
}