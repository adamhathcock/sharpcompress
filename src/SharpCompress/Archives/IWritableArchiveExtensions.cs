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
                    writableArchive.AddEntry(
                        Path.GetFileName(filePath),
                        fileInfo.OpenRead(),
                        true,
                        fileInfo.Length,
                        fileInfo.LastWriteTime
                    );
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
            return writableArchive.AddEntry(
                key,
                fileInfo.OpenRead(),
                true,
                fileInfo.Length,
                fileInfo.LastWriteTime
            );
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
