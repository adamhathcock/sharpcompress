using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public static class IWritableAsyncArchiveExtensions
{
    extension(IWritableAsyncArchive writableArchive)
    {
        public async ValueTask AddAllFromDirectoryAsync(
            string filePath,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.AllDirectories
        )
        {
            using (writableArchive.PauseEntryRebuilding())
            {
                foreach (
                    var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption)
                )
                {
                    var fileInfo = new FileInfo(path);
                    await writableArchive.AddEntryAsync(
                        path.Substring(filePath.Length),
                        fileInfo.OpenRead(),
                        true,
                        fileInfo.Length,
                        fileInfo.LastWriteTime
                    );
                }
            }
        }

        public ValueTask<IArchiveEntry> AddEntryAsync(string key, string file) =>
            writableArchive.AddEntryAsync(key, new FileInfo(file));

        public ValueTask<IArchiveEntry> AddEntryAsync(
            string key,
            Stream source,
            long size = 0,
            DateTime? modified = null
        ) => writableArchive.AddEntryAsync(key, source, false, size, modified);

        public ValueTask<IArchiveEntry> AddEntryAsync(string key, FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("FileInfo does not exist.");
            }
            return writableArchive.AddEntryAsync(
                key,
                fileInfo.OpenRead(),
                true,
                fileInfo.Length,
                fileInfo.LastWriteTime
            );
        }
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
        where TOptions : IWriterOptions
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        await writableArchive.SaveToAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }
}
