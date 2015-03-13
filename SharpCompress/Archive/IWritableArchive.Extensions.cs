using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public static class IWritableArchiveExtensions
    {
        public static void SaveTo(this IWritableArchive writableArchive,
                                                   Stream stream, CompressionType compressionType)
        {
            writableArchive.SaveTo(stream, new CompressionInfo {Type = compressionType});
        }

#if !PORTABLE && !NETFX_CORE

        public static void AddEntry(this IWritableArchive writableArchive,
                                                     string entryPath, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Could not AddEntry: " + filePath);
            }
            writableArchive.AddEntry(entryPath, new FileInfo(filePath).OpenRead(), true, fileInfo.Length,
                                     fileInfo.LastWriteTime);
        }

        public static void SaveTo(this IWritableArchive writableArchive,
                                                   string filePath, CompressionType compressionType)
        {
            writableArchive.SaveTo(new FileInfo(filePath), new CompressionInfo {Type = compressionType});
        }

        public static void SaveTo(this IWritableArchive writableArchive,
                                                   FileInfo fileInfo, CompressionType compressionType)
        {
            using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                writableArchive.SaveTo(stream, new CompressionInfo {Type = compressionType});
            }
        }

        public static void SaveTo(this IWritableArchive writableArchive,
                                                   string filePath, CompressionInfo compressionInfo)
        {
            writableArchive.SaveTo(new FileInfo(filePath), compressionInfo);
        }

        public static void SaveTo(this IWritableArchive writableArchive,
                                                   FileInfo fileInfo, CompressionInfo compressionInfo)
        {
            using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                writableArchive.SaveTo(stream, compressionInfo);
            }
        }

        public static void AddAllFromDirectory(
            this IWritableArchive writableArchive,
            string filePath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.AllDirectories)
        {
#if NET2
            foreach (var path in Directory.GetFiles(filePath, searchPattern, searchOption))
#else
            foreach (var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption))
#endif
            {
                var fileInfo = new FileInfo(path);
                writableArchive.AddEntry(path.Substring(filePath.Length), fileInfo.OpenRead(), true, fileInfo.Length,
                                         fileInfo.LastWriteTime);
            }
        }
        public static IArchiveEntry AddEntry(this IWritableArchive writableArchive, string key, FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }
            return writableArchive.AddEntry(key, fileInfo.OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime);
        }
#endif
    }
}