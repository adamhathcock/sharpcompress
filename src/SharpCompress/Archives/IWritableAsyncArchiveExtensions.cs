using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

/// <summary>Convenience overloads and default filesystem writing workflows.</summary>
public static class IWritableAsyncArchiveExtensions
{
    extension(IWritableAsyncArchive writableArchive)
    {
        public ValueTask AddAllFromDirectoryAsync(
            string directoryPath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.FileWriter.AddDirectoryAsync(
                writableArchive,
                directoryPath,
                searchPattern,
                searchOption,
                cancellationToken
            );

        public ValueTask<IArchiveEntry> AddEntryAsync(string key, string file) =>
            writableArchive.AddEntryAsync(key, new FileInfo(file));

        public ValueTask<IArchiveEntry> AddEntryAsync(
            string key,
            Stream source,
            long size = 0,
            DateTime? modified = null
        ) => writableArchive.AddEntryAsync(key, source, false, size, modified);

        public ValueTask<IArchiveEntry> AddEntryAsync(string key, FileInfo fileInfo) =>
            ClientDefaults.Client.FileWriter.AddFileAsync(writableArchive, key, fileInfo);
    }

    public static ValueTask SaveToAsync<TOptions>(
        this IWritableAsyncArchive<TOptions> writableArchive,
        string filePath,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions =>
        ClientDefaults.Client.FileWriter.SaveToFileAsync(
            writableArchive,
            new FileInfo(filePath),
            options,
            cancellationToken
        );

    public static ValueTask SaveToAsync<TOptions>(
        this IWritableAsyncArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions =>
        ClientDefaults.Client.FileWriter.SaveToFileAsync(
            writableArchive,
            fileInfo,
            options,
            cancellationToken
        );
}
