using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.IO;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

/// <summary>Filesystem writing workflows with client-local compression providers.</summary>
public sealed class ArchiveFileWriter : IArchiveFileWriter
{
    private readonly SharpCompressConfiguration configuration;

    public ArchiveFileWriter()
        : this(new SharpCompressConfiguration()) { }

    public ArchiveFileWriter(SharpCompressConfiguration configuration)
    {
        ThrowHelper.ThrowIfNull(configuration);
        this.configuration = configuration;
    }

    public void WriteFile(IWriter writer, string entryPath, FileInfo source)
    {
        ThrowHelper.ThrowIfNull(writer);
        RequireFile(source);
        using var stream = source.OpenRead();
        writer.Write(entryPath, stream, source.LastWriteTime);
    }

    public async ValueTask WriteFileAsync(
        IAsyncWriter writer,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(writer);
        cancellationToken.ThrowIfCancellationRequested();
        RequireFile(source);
        using var stream = source.OpenRead();
        await writer
            .WriteAsync(entryPath, stream, source.LastWriteTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public void WriteDirectory(
        IWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? filter = null,
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        ThrowHelper.ThrowIfNull(writer);
        var root = GetRoot(directory);
        foreach (var file in EnumerateFiles(root, searchPattern, filter, searchOption))
        {
            WriteFile(writer, GetEntryPath(root, file), new FileInfo(file));
        }
    }

    public async ValueTask WriteDirectoryAsync(
        IAsyncWriter writer,
        string directory,
        string searchPattern = "*",
        Func<string, bool>? filter = null,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(writer);
        cancellationToken.ThrowIfCancellationRequested();
        var root = GetRoot(directory);
        foreach (var file in EnumerateFiles(root, searchPattern, filter, searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteFileAsync(
                    writer,
                    GetEntryPath(root, file),
                    new FileInfo(file),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    public IArchiveEntry AddFile(IWritableArchive archive, string entryPath, FileInfo source)
    {
        ThrowHelper.ThrowIfNull(archive);
        RequireFile(source);
        var stream = source.OpenRead();
        try
        {
            // Transfer source ownership only after the archive accepts the entry.
            return archive.AddEntry(entryPath, stream, true, source.Length, source.LastWriteTime);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public async ValueTask<IArchiveEntry> AddFileAsync(
        IWritableAsyncArchive archive,
        string entryPath,
        FileInfo source,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(archive);
        cancellationToken.ThrowIfCancellationRequested();
        RequireFile(source);
        var stream = source.OpenRead();
        try
        {
            return await archive
                .AddEntryAsync(
                    entryPath,
                    stream,
                    true,
                    source.Length,
                    source.LastWriteTime,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch
        {
            await using var streamScope = stream.DisposeAsyncScope().ConfigureAwait(false);
            throw;
        }
    }

    public void AddDirectory(
        IWritableArchive archive,
        string directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.AllDirectories
    )
    {
        ThrowHelper.ThrowIfNull(archive);
        var root = GetRoot(directory);
        using var scope = archive.PauseEntryRebuilding();
        foreach (var file in EnumerateFiles(root, searchPattern, null, searchOption))
        {
            AddFile(archive, GetEntryPath(root, file), new FileInfo(file));
        }
    }

    public async ValueTask AddDirectoryAsync(
        IWritableAsyncArchive archive,
        string directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.AllDirectories,
        CancellationToken cancellationToken = default
    )
    {
        ThrowHelper.ThrowIfNull(archive);
        cancellationToken.ThrowIfCancellationRequested();
        var root = GetRoot(directory);
        using var scope = archive.PauseEntryRebuilding();
        foreach (var file in EnumerateFiles(root, searchPattern, null, searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddFileAsync(
                    archive,
                    GetEntryPath(root, file),
                    new FileInfo(file),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    public void SaveToFile<TOptions>(
        IWritableArchive<TOptions> archive,
        FileInfo file,
        TOptions options
    )
        where TOptions : IWriterOptions
    {
        ThrowHelper.ThrowIfNull(archive);
        ThrowHelper.ThrowIfNull(file);
        var preparedOptions = (TOptions)configuration.PrepareWriterOptions(options);
        using var stream = file.Open(FileMode.Create, FileAccess.Write);
        archive.SaveTo(stream, preparedOptions);
    }

    public async ValueTask SaveToFileAsync<TOptions>(
        IWritableAsyncArchive<TOptions> archive,
        FileInfo file,
        TOptions options,
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions
    {
        ThrowHelper.ThrowIfNull(archive);
        ThrowHelper.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        var preparedOptions = (TOptions)configuration.PrepareWriterOptions(options);
        using var stream = file.Open(FileMode.Create, FileAccess.Write);
        await archive.SaveToAsync(stream, preparedOptions, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireFile(FileInfo source)
    {
        ThrowHelper.ThrowIfNull(source);
        if (!source.Exists)
        {
            throw new ArgumentException(
                "Source does not exist: " + source.FullName,
                nameof(source)
            );
        }
    }

    private static string GetRoot(string directory)
    {
        directory.NotNullOrEmpty(nameof(directory));
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new ArgumentException(
                "Directory does not exist: " + directory,
                nameof(directory)
            );
        }
        return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    private static IEnumerable<string> EnumerateFiles(
        string root,
        string pattern,
        Func<string, bool>? filter,
        SearchOption option
    ) =>
        Directory.EnumerateFiles(root, pattern, option).Where(file => filter?.Invoke(file) ?? true);

    private static string GetEntryPath(string root, string file) =>
        file.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
}
