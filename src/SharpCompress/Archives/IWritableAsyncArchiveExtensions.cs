using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

public static class IWritableAsyncArchiveExtensions
{
    extension(IWritableAsyncArchive writableArchive)
    {
        public async ValueTask AddAllFromDirectoryAsync(
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
                    await writableArchive
                        .AddEntryAsync(filePath.Substring(directoryPath.Length), fileInfo)
                        .ConfigureAwait(false);
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

            return AddEntryAsyncBuffered(fileInfo);

            async ValueTask<IArchiveEntry> AddEntryAsyncBuffered(FileInfo info)
            {
                using var sourceStream = info.OpenRead();
                var bufferedStream = new MemoryStream();

                try
                {
                    await sourceStream.CopyToAsync(bufferedStream).ConfigureAwait(false);
                    bufferedStream.Position = 0;
                    return await writableArchive
                        .AddEntryAsync(key, bufferedStream, true, info.Length, info.LastWriteTime)
                        .ConfigureAwait(false);
                }
                catch
                {
#pragma warning disable VSTHRD103
                    bufferedStream.Dispose();
#pragma warning restore VSTHRD103
                    throw;
                }
            }
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
