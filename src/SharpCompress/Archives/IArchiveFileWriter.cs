using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

/// <summary>Writes filesystem sources to caller-owned writers and writable archives.</summary>
public interface IArchiveFileWriter
{
    void WriteFile(IWriter writer, string entryPath, FileInfo source);
    ValueTask WriteFileAsync(
        IAsyncWriter writer,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    );
    void WriteDirectory(
        IWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? filter = null,
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    );
    ValueTask WriteDirectoryAsync(
        IAsyncWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? filter = null,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default
    );
    IArchiveEntry AddFile(IWritableArchive archive, string entryPath, FileInfo source);
    ValueTask<IArchiveEntry> AddFileAsync(
        IWritableAsyncArchive archive,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    );
    void AddDirectory(
        IWritableArchive archive,
        string directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.AllDirectories
    );
    ValueTask AddDirectoryAsync(
        IWritableAsyncArchive archive,
        string directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.AllDirectories,
        CancellationToken cancellationToken = default
    );
    void SaveToFile<TOptions>(IWritableArchive<TOptions> archive, FileInfo file, TOptions options)
        where TOptions : IWriterOptions;
    ValueTask SaveToFileAsync<TOptions>(
        IWritableAsyncArchive<TOptions> archive,
        FileInfo file,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions;
}
