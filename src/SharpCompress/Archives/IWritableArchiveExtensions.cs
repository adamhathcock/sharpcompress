using System;
using System.IO;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

public static class IWritableArchiveExtensions
{
    extension(IWritableArchive writableArchive)
    {
        public void AddAllFromDirectory(
            string directoryPath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories
        )
        {
            using (writableArchive.PauseEntryRebuilding())
            {
                foreach (
                    var filePath in Directory.EnumerateFiles(
                        directoryPath,
                        searchPattern,
                        searchOption
                    )
                )
                {
                    var fileInfo = new FileInfo(filePath);
                    writableArchive.AddEntry(filePath.Substring(directoryPath.Length), fileInfo);
                }
            }
        }

        public IArchiveEntry AddEntry(string key, string file) =>
            writableArchive.AddEntry(key, new FileInfo(file));

        public IArchiveEntry AddEntry(
            string key,
            Stream source,
            long size = 0,
            DateTime? modified = null
        ) => writableArchive.AddEntry(key, source, false, size, modified);

        public IArchiveEntry AddEntry(string key, FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }

            using var sourceStream = fileInfo.OpenRead();
            var bufferedStream = new MemoryStream();

            try
            {
                sourceStream.CopyTo(bufferedStream);
                bufferedStream.Position = 0;
                return writableArchive.AddEntry(
                    key,
                    bufferedStream,
                    true,
                    fileInfo.Length,
                    fileInfo.LastWriteTime
                );
            }
            catch
            {
                bufferedStream.Dispose();
                throw;
            }
        }
    }

    public static void SaveTo<TOptions>(
        this IWritableArchive<TOptions> writableArchive,
        string filePath,
        TOptions options
    )
        where TOptions : IWriterOptions => writableArchive.SaveTo(new FileInfo(filePath), options);

    public static void SaveTo<TOptions>(
        this IWritableArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options
    )
        where TOptions : IWriterOptions
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        writableArchive.SaveTo(stream, options);
    }
}
