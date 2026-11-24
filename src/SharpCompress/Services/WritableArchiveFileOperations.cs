using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pure.DI;
using SharpCompress.Archives;
using SharpCompress.Common.Options;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed class WritableArchiveFileOperations : IWritableArchiveFileOperations
{
    public void AddAllFromDirectory(
        IWritableArchive writableArchive,
        string directoryPath,
        string searchPattern,
        SearchOption searchOption
    )
    {
        using (writableArchive.PauseEntryRebuilding())
        {
            foreach (
                var filePath in Directory.EnumerateFiles(directoryPath, searchPattern, searchOption)
            )
            {
                var fileInfo = new FileInfo(filePath);
                // The stream follows a symlink; FileInfo.Length is the link's own size on Unix
                var stream = fileInfo.OpenRead();
                writableArchive.AddEntry(
                    filePath.Substring(directoryPath.Length),
                    stream,
                    true,
                    stream.Length,
                    fileInfo.LastWriteTime
                );
            }
        }
    }

    public async ValueTask AddAllFromDirectoryAsync(
        IWritableAsyncArchive writableArchive,
        string directoryPath,
        string searchPattern,
        SearchOption searchOption,
        CancellationToken cancellationToken = default
    )
    {
        using (writableArchive.PauseEntryRebuilding())
        {
            foreach (
                var filePath in Directory.EnumerateFiles(directoryPath, searchPattern, searchOption)
            )
            {
                var fileInfo = new FileInfo(filePath);
                var stream = fileInfo.OpenRead();
                await writableArchive
                    .AddEntryAsync(
                        filePath.Substring(directoryPath.Length),
                        stream,
                        true,
                        stream.Length,
                        fileInfo.LastWriteTime,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    public IArchiveEntry AddEntry(IWritableArchive writableArchive, string key, FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            throw new ArgumentException("FileInfo does not exist.");
        }
        var stream = fileInfo.OpenRead();
        return writableArchive.AddEntry(key, stream, true, stream.Length, fileInfo.LastWriteTime);
    }

    public ValueTask<IArchiveEntry> AddEntryAsync(
        IWritableAsyncArchive writableArchive,
        string key,
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
    {
        if (!fileInfo.Exists)
        {
            throw new ArgumentException("FileInfo does not exist.");
        }
        var stream = fileInfo.OpenRead();
        return writableArchive.AddEntryAsync(
            key,
            stream,
            true,
            stream.Length,
            fileInfo.LastWriteTime,
            cancellationToken
        );
    }

    public void SaveTo<TOptions>(
        IWritableArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options
    )
        where TOptions : IWriterOptions
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        writableArchive.SaveTo(stream, options);
    }

    public async ValueTask SaveToAsync<TOptions>(
        IWritableAsyncArchive<TOptions> writableArchive,
        FileInfo fileInfo,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions
    {
        var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
        await writableArchive.SaveToAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }
}
