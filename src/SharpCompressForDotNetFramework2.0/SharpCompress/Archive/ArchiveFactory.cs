using System;
using System.IO;
using SharpCompress.Archive.GZip;
using SharpCompress.Archive.Rar;
using SharpCompress.Archive.SevenZip;
using SharpCompress.Archive.Tar;
using SharpCompress.Archive.Zip;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public class ArchiveFactory
    {
        /// <summary>
        /// Opens an Archive for random access
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static IArchive Open(Stream stream, Options options = Options.KeepStreamsOpen)
        {
            stream.CheckNotNull("stream");
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }

            if (ZipArchive.IsZipFile(stream, null))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return ZipArchive.Open(stream, options, null);
            }
            stream.Seek(0, SeekOrigin.Begin);
            if (RarArchive.IsRarFile(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return RarArchive.Open(stream, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            if (TarArchive.IsTarFile(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return TarArchive.Open(stream, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            if (SevenZipArchive.IsSevenZipFile(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return SevenZipArchive.Open(stream, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            if (GZipArchive.IsGZipFile(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return GZipArchive.Open(stream, options);
            }
            throw new InvalidOperationException("Cannot determine compressed stream type.");
        }

        public static IArchive Create(ArchiveType type)
        {
            switch (type)
            {
                case ArchiveType.Zip:
                    {
                        return ZipArchive.Create();
                    }
                case ArchiveType.Tar:
                    {
                        return TarArchive.Create();
                    }
                default:
                    {
                        throw new NotSupportedException("Cannot create Archives of type: " + type);
                    }
            }
        }

#if !PORTABLE
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        public static IArchive Open(string filePath)
        {
            return Open(filePath, Options.None);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        public static IArchive Open(FileInfo fileInfo)
        {
            return Open(fileInfo, Options.None);
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static IArchive Open(string filePath, Options options)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static IArchive Open(FileInfo fileInfo, Options options)
        {
            fileInfo.CheckNotNull("fileInfo");
            using (var stream = fileInfo.OpenRead())
            {
                if (ZipArchive.IsZipFile(stream, null))
                {
                    stream.Dispose();
                    return ZipArchive.Open(fileInfo, options, null);
                }
                stream.Seek(0, SeekOrigin.Begin);
                if (RarArchive.IsRarFile(stream, Options.LookForHeader | Options.KeepStreamsOpen))
                {
                    stream.Dispose();
                    return RarArchive.Open(fileInfo, options);
                }
                stream.Seek(0, SeekOrigin.Begin);
                if (TarArchive.IsTarFile(stream))
                {
                    stream.Dispose();
                    return TarArchive.Open(fileInfo, options);
                }
                stream.Seek(0, SeekOrigin.Begin);
                if (SevenZipArchive.IsSevenZipFile(stream))
                {
                    stream.Dispose();
                    return SevenZipArchive.Open(fileInfo, options);
                }
                stream.Seek(0, SeekOrigin.Begin);
                if (GZipArchive.IsGZipFile(stream))
                {
                    stream.Dispose();
                    return GZipArchive.Open(fileInfo, options);
                }
                throw new InvalidOperationException("Cannot determine compressed stream type.");
            }
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteToDirectory(string sourceArchive, string destinationDirectory,
            ExtractOptions options = ExtractOptions.Overwrite)
        {
            using (IArchive archive = Open(sourceArchive))
            {
                foreach (IArchiveEntry entry in archive.Entries)
                {
                    entry.WriteToDirectory(destinationDirectory, options);
                }
            }
        }
#endif
    }
}
