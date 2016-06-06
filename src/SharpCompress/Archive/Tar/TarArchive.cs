using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Reader;
using SharpCompress.Reader.Tar;
using SharpCompress.Writer.Tar;

namespace SharpCompress.Archive.Tar
{
    public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
    {
#if !NO_FILE
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        public static TarArchive Open(string filePath)
        {
            return Open(filePath, Options.None);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        public static TarArchive Open(FileInfo fileInfo)
        {
            return Open(fileInfo, Options.None);
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static TarArchive Open(string filePath, Options options)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static TarArchive Open(FileInfo fileInfo, Options options)
        {
            fileInfo.CheckNotNull("fileInfo");
            return new TarArchive(fileInfo, options);
        }
#endif

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        public static TarArchive Open(Stream stream)
        {
            stream.CheckNotNull("stream");
            return Open(stream, Options.None);
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        public static TarArchive Open(Stream stream, Options options)
        {
            stream.CheckNotNull("stream");
            return new TarArchive(stream, options);
        }

#if !NO_FILE
        public static bool IsTarFile(string filePath)
        {
            return IsTarFile(new FileInfo(filePath));
        }

        public static bool IsTarFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }
            using (Stream stream = fileInfo.OpenRead())
            {
                return IsTarFile(stream);
            }
        }
#endif

        public static bool IsTarFile(Stream stream)
        {
            try
            {
                TarHeader tar = new TarHeader();
                tar.Read(new BinaryReader(stream));
                return tar.Name.Length > 0 && Enum.IsDefined(typeof (EntryType), tar.EntryType);
            }
            catch
            {
            }
            return false;
        }

#if !NO_FILE
        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        internal TarArchive(FileInfo fileInfo, Options options)
            : base(ArchiveType.Tar, fileInfo, options)
        {
        }

        protected override IEnumerable<TarVolume> LoadVolumes(FileInfo file, Options options)
        {
            if (FlagUtility.HasFlag(options, Options.KeepStreamsOpen))
            {
                options = (Options)FlagUtility.SetFlag(options, Options.KeepStreamsOpen, false);
            }
            return new TarVolume(file.OpenRead(), options).AsEnumerable();
        }
#endif

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        internal TarArchive(Stream stream, Options options)
            : base(ArchiveType.Tar, stream, options)
        {
        }

        internal TarArchive()
            : base(ArchiveType.Tar)
        {
        }

        protected override IEnumerable<TarVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return new TarVolume(streams.First(), options).AsEnumerable();
        }

        protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
        {
            Stream stream = volumes.Single().Stream;
            TarHeader previousHeader = null;
            foreach (TarHeader header in TarHeaderFactory.ReadHeader(StreamingMode.Seekable, stream))
            {
                if (header != null)
                {
                    if (header.EntryType == EntryType.LongName)
                    {
                        previousHeader = header;
                    }
                    else
                    {
                        if (previousHeader != null)
                        {
                            var entry = new TarArchiveEntry(this, new TarFilePart(previousHeader, stream),
                                                            CompressionType.None);

                            var oldStreamPos = stream.Position;

                            using(var entryStream = entry.OpenEntryStream())
                            using(var memoryStream = new MemoryStream())
                            {
                                entryStream.TransferTo(memoryStream);
                                memoryStream.Position = 0;
                                var bytes = memoryStream.ToArray();

                                header.Name = ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length).TrimNulls();
                            }

                            stream.Position = oldStreamPos;

                            previousHeader = null;
                        }
                        yield return new TarArchiveEntry(this, new TarFilePart(header, stream), CompressionType.None);
                    }
                }
            }
        }

        public static TarArchive Create()
        {
            return new TarArchive();
        }

        protected override TarArchiveEntry CreateEntryInternal(string filePath, Stream source,
                                                       long size, DateTime? modified, bool closeStream)
        {
            return new TarWritableArchiveEntry(this, source, CompressionType.Unknown, filePath, size, modified,
                                               closeStream);
        }

        protected override void SaveTo(Stream stream, CompressionInfo compressionInfo,
                                       IEnumerable<TarArchiveEntry> oldEntries,
                                       IEnumerable<TarArchiveEntry> newEntries)
        {
            using (var writer = new TarWriter(stream, compressionInfo))
            {
                foreach (var entry in oldEntries.Concat(newEntries)
                                                .Where(x => !x.IsDirectory))
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, entryStream, entry.LastModifiedTime, entry.Size);
                    }
                }
            }
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            var stream = Volumes.Single().Stream;
            stream.Position = 0;
            return TarReader.Open(stream);
        }
    }
}