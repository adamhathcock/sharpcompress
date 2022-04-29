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
                        using (Stream prt2z = files[1].OpenRead())
                        {
                            try
                            {
                                prt2z.Position += 4; //skip the POST_DATA_DESCRIPTOR to prevent an exception
                                if (ZipArchive.IsZipFile(prt2z)) //if part2 is a zip then it's multi not split  zip, z01,z02... zip is moved to the end when opened
                                    return ZipArchive.Open(fileInfos.Select(a => a.OpenRead()), options);
                            }
                            catch { }
                        }
                        return ZipArchive.Open(new SplitStream(files), options);
                    case ArchiveType.SevenZip:
                        return SevenZipArchive.Open(new SplitStream(files), options);
                    case ArchiveType.GZip:
                        return GZipArchive.Open(new SplitStream(files), options);
                    case ArchiveType.Rar:
                        using (Stream prt2 = files[1].OpenRead())
                        {
                            try
                            {
                                if (RarArchive.IsRarFile(prt2, options)) //if part2 is a rar then it's multi not split
                                    return RarArchive.Open(files.Select(f => File.OpenRead(f.FullName))); //multipart read
                            }
                            catch { }
                        }
                        return RarArchive.Open(new SplitStream(files), options);
                    case ArchiveType.Tar:
                        return TarArchive.Open(new SplitStream(files), options);
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
    }
}
