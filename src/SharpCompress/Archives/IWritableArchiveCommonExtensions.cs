using System;
using System.IO;

namespace SharpCompress.Archives;

public static class IWritableArchiveCommonExtensions
{
    public static void AddEntry(
        this IWritableArchive writableArchive,
        string entryPath,
        string filePath
    )
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Could not AddEntry: " + filePath);
        }
        writableArchive.AddEntry(
            entryPath,
            new FileInfo(filePath).OpenRead(),
            true,
            fileInfo.Length,
            fileInfo.LastWriteTime
        );
    }

    public static void AddAllFromDirectory(
        this IWritableArchiveCommon writableArchive,
        string filePath,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.AllDirectories
    )
    {
        using (writableArchive.PauseEntryRebuilding())
        {
            foreach (var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption))
            {
                var fileInfo = new FileInfo(path);
                writableArchive.AddEntry(
                    path.Substring(filePath.Length),
                    fileInfo.OpenRead(),
                    true,
                    fileInfo.Length,
                    fileInfo.LastWriteTime
                );
            }
        }
    }

    public static IArchiveEntry AddEntry(
        this IWritableArchiveCommon writableArchive,
        string key,
        string file
    ) => AddEntry(writableArchive, key, new FileInfo(file));

    public static IArchiveEntry AddEntry(
        this IWritableArchiveCommon writableArchive,
        string key,
        FileInfo fileInfo
    )
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
