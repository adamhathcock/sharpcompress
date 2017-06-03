using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public static class AbstractWritableArchiveExtensions
    {

       public static void SaveTo<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
             Stream stream, CompressionType compressionType)
          where TEntry : IArchiveEntry
          where TVolume : IVolume
       {
          writableArchive.SaveTo(stream, new CompressionInfo { Type = compressionType });
       }
#if !PORTABLE

        public static void AddEntry<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
            string entryPath, string filePath)
            where TEntry : IArchiveEntry
            where TVolume : IVolume
       {
           var fileInfo = new FileInfo(filePath);
           if (!fileInfo.Exists)
           {
              throw new FileNotFoundException("Could not AddEntry: " + filePath);
           }
           writableArchive.AddEntry(entryPath, new FileInfo(filePath).OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime);
        }

        public static void SaveTo<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
            string filePath, CompressionType compressionType)
            where TEntry : IArchiveEntry
            where TVolume : IVolume
        {
           writableArchive.SaveTo(new FileInfo(filePath), new CompressionInfo { Type = compressionType });
        }

        public static void SaveTo<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
             FileInfo fileInfo, CompressionType compressionType)
            where TEntry : IArchiveEntry
            where TVolume : IVolume
        {
            using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                writableArchive.SaveTo(stream, new CompressionInfo { Type = compressionType });
            }
        }

        public static void SaveTo<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
             string filePath, CompressionInfo compressionInfo)
           where TEntry : IArchiveEntry
           where TVolume : IVolume
        {
           writableArchive.SaveTo(new FileInfo(filePath), compressionInfo);
        }

        public static void SaveTo<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
             FileInfo fileInfo, CompressionInfo compressionInfo)
           where TEntry : IArchiveEntry
           where TVolume : IVolume
        {
           using (var stream = fileInfo.Open(FileMode.Create, FileAccess.Write))
           {
              writableArchive.SaveTo(stream, compressionInfo);
           }
        }

        public static void AddAllFromDirectory<TEntry, TVolume>(this AbstractWritableArchive<TEntry, TVolume> writableArchive,
            string filePath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.AllDirectories)
            where TEntry : IArchiveEntry
            where TVolume : IVolume
        {
#if THREEFIVE
            foreach (var path in Directory.GetFiles(filePath, searchPattern, searchOption))
#else
            foreach (var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption))
#endif

            {
               var fileInfo = new FileInfo(path);
               writableArchive.AddEntry(path.Substring(filePath.Length), fileInfo.OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime);
            }
        }
#endif
    }
}
