using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives
{
    public static class ArchiveFactory
    {
        /// <summary>
        /// Opens an Archive for random access
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        /// <returns></returns>
        public static IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            stream.CheckNotNull(nameof(stream));
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }

            readerOptions ??= new ReaderOptions();

            return FindArchiveFactory(stream).Open(stream, readerOptions);
        }

        public static IWritableArchive Create(ArchiveType type)
        {
            return type switch
            {
                ArchiveType.Zip => ZipArchive.Create(),
                ArchiveType.Tar => TarArchive.Create(),
                ArchiveType.GZip => GZipArchive.Create(),
                _ => throw new NotSupportedException("Cannot create Archives of type: " + type)
            };
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static IArchive Open(string filePath, ReaderOptions? options = null)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            return Open(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static IArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
        {
            fileInfo.CheckNotNull(nameof(fileInfo));
            options ??= new ReaderOptions { LeaveStreamOpen = false };

            return FindArchiveFactory(fileInfo).Open(fileInfo, options);
        }

        /// <summary>
        /// Constructor with IEnumerable FileInfo objects, multi and split support.
        /// </summary>
        /// <param name="fileInfos"></param>
        /// <param name="options"></param>
        public static IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? options = null)
        {
            fileInfos.CheckNotNull(nameof(fileInfos));
            FileInfo[] files = fileInfos.ToArray();
            if (files.Length == 0)
                throw new InvalidOperationException("No files to open");
            FileInfo fileInfo = files[0];
            if (files.Length == 1)
                return Open(fileInfo, options);

            fileInfo.CheckNotNull(nameof(fileInfo));
            options ??= new ReaderOptions { LeaveStreamOpen = false };

            return FindArchiveFactory(fileInfo).Open(fileInfos, options);
        }

        /// <summary>
        /// Constructor with IEnumerable FileInfo objects, multi and split support.
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="options"></param>
        public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? options = null)
        {
            streams.CheckNotNull(nameof(streams));
            Stream[] streamsArray = streams.ToArray();
            if (streamsArray.Length == 0)
                throw new InvalidOperationException("No streams");
            Stream firstStream = streamsArray[0];
            if (streamsArray.Length == 1)
                return Open(firstStream, options);

            firstStream.CheckNotNull(nameof(firstStream));
            options ??= new ReaderOptions();

            return FindArchiveFactory(firstStream).Open(streamsArray, options);            
        }        

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteToDirectory(string sourceArchive, string destinationDirectory,
                                            ExtractionOptions? options = null)
        {
            using IArchive archive = Open(sourceArchive);
            foreach (IArchiveEntry entry in archive.Entries)
            {
                entry.WriteToDirectory(destinationDirectory, options);
            }
        }

        private static IArchiveFactory FindArchiveFactory(FileInfo finfo)
        {
            finfo.CheckNotNull(nameof(finfo));
            using (Stream stream = finfo.OpenRead())
            {
                return FindArchiveFactory(stream);
            }
        }

        private static IArchiveFactory FindArchiveFactory(Stream stream)
        {
            stream.CheckNotNull(nameof(stream));
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }

            var factores = Factories.Factory.Factories.OfType<IArchiveFactory>();

            long startPosition = stream.Position;

            foreach (var factory in factores)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);

                if (factory.IsArchive(stream))
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);

                    return factory;
                }
            }

            var extensions = string.Join(", ", factores.Select(item => item.Name));

            throw new InvalidOperationException($"Cannot determine compressed stream type. Supported Archive Formats: {extensions}");
        }

        public static bool IsArchive(string filePath, out ArchiveType? type)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            using (Stream s = File.OpenRead(filePath))
                return IsArchive(s, out type);
        }

        private static bool IsArchive(Stream stream, out ArchiveType? type)
        {
            type = null;
            stream.CheckNotNull(nameof(stream));

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }

            var startPosition = stream.Position;

            foreach(var factory in Factories.Factory.Factories)
            {
                stream.Position = startPosition;

                if (factory.IsArchive(stream, null))
                {
                    type = factory.KnownArchiveType;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// From a passed in archive (zip, rar, 7z, 001), return all parts.
        /// </summary>
        /// <param name="part1"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetFileParts(string part1)
        {
            part1.CheckNotNullOrEmpty(nameof(part1));
            return GetFileParts(new FileInfo(part1)).Select(a => a.FullName);
        }

        /// <summary>
        /// From a passed in archive (zip, rar, 7z, 001), return all parts.
        /// </summary>
        /// <param name="part1"></param>
        /// <returns></returns>
        public static IEnumerable<FileInfo> GetFileParts(FileInfo part1)
        {
            part1.CheckNotNull(nameof(part1));
            yield return part1;
            int i = 1;

            FileInfo? part = RarArchiveVolumeFactory.GetFilePart(i++, part1);
            if (part != null)
            {
                yield return part;
                while ((part = RarArchiveVolumeFactory.GetFilePart(i++, part1)) != null) //tests split too
                    yield return part;
            }
            else
            {
                i = 1;
                while ((part = ZipArchiveVolumeFactory.GetFilePart(i++, part1)) != null) //tests split too
                    yield return part;
            }
        }
    }
}
