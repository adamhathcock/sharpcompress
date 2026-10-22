using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.Services;

namespace SharpCompress.Archives;

public static class IWritableAsyncArchiveExtensions
{
    extension(IWritableAsyncArchive writableArchive)
    {
        public ValueTask AddAllFromDirectoryAsync(
            string directoryPath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories
        ) =>
            LibraryDefaults.ArchiveFiles.AddAllFromDirectoryAsync(
                writableArchive,
                directoryPath,
                searchPattern,
                searchOption
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
            LibraryDefaults.ArchiveFiles.AddEntryAsync(writableArchive, key, fileInfo);
    }

    public static ValueTask SaveToAsync<TOptions>(
        this IWritableAsyncArchive<TOptions> writableArchive,
        string filePath,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions =>
        writableArchive.SaveToAsync(new FileInfo(filePath), options, cancellationToken);

    public static async ValueTask SaveToAsync<TOptions>(
        this IWritableAsyncArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions =>
        await LibraryDefaults
            .ArchiveFiles.SaveToAsync(writableArchive, fileInfo, options, cancellationToken)
            .ConfigureAwait(false);
}
