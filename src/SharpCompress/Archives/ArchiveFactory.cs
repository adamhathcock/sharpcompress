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

            ArchiveType? type;
            IsArchive(stream, out type); //test and reset stream position

            if (type != null)
            {
                switch (type.Value)
                {
                    case ArchiveType.Zip:
                        return ZipArchive.Open(stream, readerOptions);
                    case ArchiveType.SevenZip:
                        return SevenZipArchive.Open(stream, readerOptions);
                    case ArchiveType.GZip:
                        return GZipArchive.Open(stream, readerOptions);
                    case ArchiveType.Rar:
                        return RarArchive.Open(stream, readerOptions);
                    case ArchiveType.Tar:
                        return TarArchive.Open(stream, readerOptions);
                }
            }
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip, LZip");
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

            ArchiveType? type;
            using (Stream stream = fileInfo.OpenRead())
            {
                IsArchive(stream, out type); //test and reset stream position

                if (type != null)
                {
                    switch (type.Value)
                    {
                        case ArchiveType.Zip:
                            return ZipArchive.Open(fileInfo, options);
                        case ArchiveType.SevenZip:
                            return SevenZipArchive.Open(fileInfo, options);
                        case ArchiveType.GZip:
                            return GZipArchive.Open(fileInfo, options);
                        case ArchiveType.Rar:
                            return RarArchive.Open(fileInfo, options);
                        case ArchiveType.Tar:
                            return TarArchive.Open(fileInfo, options);
                    }
                }
            }
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip");
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

            ArchiveType? type;
            using (Stream stream = fileInfo.OpenRead())
                IsArchive(stream, out type); //test and reset stream position

            if (type != null)
            {
                switch (type.Value)
                {
                    case ArchiveType.Zip:
                        return ZipArchive.Open(files, options);
                    case ArchiveType.SevenZip:
                        return SevenZipArchive.Open(files, options);
                    case ArchiveType.GZip:
                        return GZipArchive.Open(files, options);
                    case ArchiveType.Rar:
                        return RarArchive.Open(files, options);
                    case ArchiveType.Tar:
                        return TarArchive.Open(files, options);
                }
            }
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip");
        }

        /// <summary>
        /// Constructor with IEnumerable FileInfo objects, multi and split support.
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="options"></param>
        public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? options = null)
        {
            streams.CheckNotNull(nameof(streams));
            if (streams.Count() == 0)
                throw new InvalidOperationException("No streams");
            if (streams.Count() == 1)
                return Open(streams.First(), options);


            options ??= new ReaderOptions();

            ArchiveType? type;
            using (Stream stream = streams.First())
                IsArchive(stream, out type); //test and reset stream position

            if (type != null)
            {
                switch (type.Value)
                {
                    case ArchiveType.Zip:
                        return ZipArchive.Open(streams, options);
                    case ArchiveType.SevenZip:
                        return SevenZipArchive.Open(streams, options);
                    case ArchiveType.GZip:
                        return GZipArchive.Open(streams, options);
                    case ArchiveType.Rar:
                        return RarArchive.Open(streams, options);
                    case ArchiveType.Tar:
                        return TarArchive.Open(streams, options);
                }
            }
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip");
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
            if (ZipArchive.IsZipFile(stream, null))
                type = ArchiveType.Zip;
            stream.Seek(0, SeekOrigin.Begin);
            if (type == null)
            {
                if (SevenZipArchive.IsSevenZipFile(stream))
                    type = ArchiveType.SevenZip;
                stream.Seek(0, SeekOrigin.Begin);
            }
            if (type == null)
            {
                if (GZipArchive.IsGZipFile(stream))
                    type = ArchiveType.GZip;
                stream.Seek(0, SeekOrigin.Begin);
            }
            if (type == null)
            {
                if (RarArchive.IsRarFile(stream))
                    type = ArchiveType.Rar;
                stream.Seek(0, SeekOrigin.Begin);
            }
            if (type == null)
            {
                if (TarArchive.IsTarFile(stream))
                    type = ArchiveType.Tar;
                stream.Seek(0, SeekOrigin.Begin);
            }
            if (type == null) //test multipartzip as it could find zips in other non compressed archive types?
            {
                if (ZipArchive.IsZipMulti(stream)) //test the zip (last) file of a multipart zip
                    type = ArchiveType.Zip;
                stream.Seek(0, SeekOrigin.Begin);
            }

            return type != null;
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
