using System;
using System.IO;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

/// <summary>Convenience overloads and default filesystem writing workflows.</summary>
public static class IWritableArchiveExtensions
{
    extension(IWritableArchive writableArchive)
    {
        public void AddAllFromDirectory(
            string directoryPath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories
        ) =>
            ClientDefaults.Client.FileWriter.AddDirectory(
                writableArchive,
                directoryPath,
                searchPattern,
                searchOption
            );

        public IArchiveEntry AddEntry(string key, string file) =>
            writableArchive.AddEntry(key, new FileInfo(file));

        public IArchiveEntry AddEntry(
            string key,
            Stream source,
            long size = 0,
            DateTime? modified = null
        ) => writableArchive.AddEntry(key, source, false, size, modified);

        public IArchiveEntry AddEntry(string key, FileInfo fileInfo) =>
            ClientDefaults.Client.FileWriter.AddFile(writableArchive, key, fileInfo);
    }

    public static void SaveTo<TOptions>(
        this IWritableArchive<TOptions> writableArchive,
        string filePath,
        TOptions options
    )
        where TOptions : IWriterOptions =>
        ClientDefaults.Client.FileWriter.SaveToFile(
            writableArchive,
            new FileInfo(filePath),
            options
        );

    public static void SaveTo<TOptions>(
        this IWritableArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options
    )
        where TOptions : IWriterOptions =>
        ClientDefaults.Client.FileWriter.SaveToFile(writableArchive, fileInfo, options);
}
