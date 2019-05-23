#if !NO_FILE
using System;
#endif
using System.IO;
using System.Linq;
using SharpCompress.Writers;

namespace SharpCompress.Archives
{
    public static class IWritableArchiveExtensions
    {
#if !NO_FILE

        public static void AddEntry(
            this IWritableArchive writableArchive,
            string entryPath,
            string filePath,
            bool contentOnly = false)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Could not AddEntry: " + filePath);
            }

            var modified = contentOnly ? (DateTime?)null : fileInfo.LastWriteTime;
            writableArchive.AddEntry(entryPath, new FileInfo(filePath).OpenRead(), true, fileInfo.Length, modified);
        }

        public static void SaveTo(this IWritableArchive writableArchive, string filePath, WriterOptions options)
        {
            writableArchive.SaveTo(new FileInfo(filePath), options);
        }

        public static void SaveTo(this IWritableArchive writableArchive, FileInfo fileInfo, WriterOptions options)
        {
            using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                writableArchive.SaveTo(stream, options);
            }
        }

        public static void AddAllFromDirectory(
            this IWritableArchive writableArchive,
            string filePath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories,
            bool contentOnly = false)
        {
#if NET35
            var paths = Directory.GetFiles(filePath, searchPattern, searchOption);
#else
            var paths = Directory.EnumerateFiles(filePath, searchPattern, searchOption);
#endif
            if (contentOnly)
            {
#if NET35
                Array.Sort(paths);
#else
                var pathList = paths.ToList();
                pathList.Sort();
                paths = pathList;
#endif
            }

            foreach (var path in paths)
            {
                var fileInfo = new FileInfo(path);
                var modified = contentOnly ? (DateTime?)null : fileInfo.LastWriteTime;
                writableArchive.AddEntry(path.Substring(filePath.Length), fileInfo.OpenRead(), true, fileInfo.Length, modified);
            }
        }
        public static IArchiveEntry AddEntry(this IWritableArchive writableArchive, string key, FileInfo fileInfo, bool contentOnly = false)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }

            var modified = contentOnly ? (DateTime?)null : fileInfo.LastWriteTime;
            return writableArchive.AddEntry(key, fileInfo.OpenRead(), true, fileInfo.Length, modified);
        }

#endif
    }
}