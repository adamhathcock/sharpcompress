using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.LZip;
using SharpCompress.Readers;
using SharpCompress.Readers.LZip;
using SharpCompress.Writers;
using SharpCompress.Writers.LZip;

namespace SharpCompress.Archives.LZip
{
    public class LZipArchive : AbstractWritableArchive<LZipArchiveEntry, LZipVolume>
    {
#if !NO_FILE
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="readerOptions"></param>
        public static LZipArchive Open(string filePath, ReaderOptions readerOptions = null)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        public static LZipArchive Open(FileInfo fileInfo, ReaderOptions readerOptions = null)
        {
            fileInfo.CheckNotNull("fileInfo");
            return new LZipArchive(fileInfo, readerOptions ?? new ReaderOptions());
        }
#endif
        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        public static LZipArchive Open(Stream stream, ReaderOptions readerOptions = null)
        {
            stream.CheckNotNull("stream");
            return new LZipArchive(stream, readerOptions ?? new ReaderOptions());
        }

        public static LZipArchive Create()
        {
            return new LZipArchive();
        }

#if !NO_FILE

/// <summary>
/// Constructor with a FileInfo object to an existing file.
/// </summary>
/// <param name="fileInfo"></param>
/// <param name="options"></param>
        internal LZipArchive(FileInfo fileInfo, ReaderOptions options)
            : base(ArchiveType.GZip, fileInfo, options)
        {
        }

        protected override IEnumerable<LZipVolume> LoadVolumes(FileInfo file)
        {
            return new LZipVolume(file, ReaderOptions).AsEnumerable();
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
                SaveTo(stream, new WriterOptions(CompressionType.GZip));
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
            {
                return false;
            }

            if (n != 10)
            {
                return false;
            }

            if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        internal LZipArchive(Stream stream, ReaderOptions options)
            : base(ArchiveType.GZip, stream, options)
        {
        }

        internal LZipArchive()
            : base(ArchiveType.GZip)
        {
        }

        protected override LZipArchiveEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified,
                                                                bool closeStream)
        {
            if (Entries.Any())
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            return new LZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
        }

        protected override void SaveTo(Stream stream, WriterOptions options,
                                       IEnumerable<LZipArchiveEntry> oldEntries,
                                       IEnumerable<LZipArchiveEntry> newEntries)
        {
            if (Entries.Count > 1)
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            using (var writer = new LZipWriter(stream))
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

        protected override IEnumerable<LZipVolume> LoadVolumes(IEnumerable<Stream> streams)
        {
            return new LZipVolume(streams.First(), ReaderOptions).AsEnumerable();
        }

        protected override IEnumerable<LZipArchiveEntry> LoadEntries(IEnumerable<LZipVolume> volumes)
        {
            Stream stream = volumes.Single().Stream;
            yield return new LZipArchiveEntry(this, new LZipFilePart(stream));
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            var stream = Volumes.Single().Stream;
            stream.Position = 0;
            return LZipReader.Open(stream);
        }
    }
}