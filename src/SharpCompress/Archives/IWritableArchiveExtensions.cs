using System;
using System.IO;
using SharpCompress.Common.Options;
using SharpCompress.Services;

namespace SharpCompress.Archives;

public static class IWritableArchiveExtensions
{
    extension(IWritableArchive writableArchive)
    {
        public void AddAllFromDirectory(
            string directoryPath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories
        ) =>
            LibraryDefaults.Composition.FileWriter.AddAllFromDirectory(
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
            LibraryDefaults.Composition.FileWriter.AddEntry(writableArchive, key, fileInfo);
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
        where TOptions : IWriterOptions =>
        LibraryDefaults.Composition.FileWriter.SaveTo(writableArchive, fileInfo, options);
}
