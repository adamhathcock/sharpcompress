using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Writers;

namespace SharpCompress.Archives
{
    public static class IWritableArchiveExtensions
    {
        public static async ValueTask AddEntryAsync(this IWritableArchive writableArchive,
                                         string entryPath, string filePath, 
                                         CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Could not AddEntry: " + filePath);
            }
            await writableArchive.AddEntryAsync(entryPath, new FileInfo(filePath).OpenRead(), true, fileInfo.Length,
                                     fileInfo.LastWriteTime, cancellationToken);
        }

        public static Task SaveToAsync(this IWritableArchive writableArchive, string filePath, WriterOptions options, CancellationToken cancellationToken = default)
        {
            return writableArchive.SaveToAsync(new FileInfo(filePath), options, cancellationToken);
        }

        public static async Task SaveToAsync(this IWritableArchive writableArchive, FileInfo fileInfo, WriterOptions options, CancellationToken cancellationToken = default)
        {
            await using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
            await writableArchive.SaveToAsync(stream, options, cancellationToken);
        }

        public static async ValueTask AddAllFromDirectoryAsync(
            this IWritableArchive writableArchive,
            string filePath, string searchPattern = "*.*", 
            SearchOption searchOption = SearchOption.AllDirectories,
            CancellationToken cancellationToken = default)
        {
            await using (writableArchive.PauseEntryRebuilding())
            {
                foreach (var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption))
                {
                    var fileInfo = new FileInfo(path);
                    await writableArchive.AddEntryAsync(path.Substring(filePath.Length), fileInfo.OpenRead(), true, fileInfo.Length,
                                            fileInfo.LastWriteTime,
                                            cancellationToken);
                }
            }
        }
        public static ValueTask<IArchiveEntry> AddEntryAsync(this IWritableArchive writableArchive, string key, FileInfo fileInfo,
                                                  CancellationToken cancellationToken = default)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }
            return writableArchive.AddEntryAsync(key, fileInfo.OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime, cancellationToken);
        }
    }
}