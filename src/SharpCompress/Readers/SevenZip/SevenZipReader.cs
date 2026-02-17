using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.IO;

namespace SharpCompress.Readers.SevenZip;

/// <summary>
/// Public 7Zip reader entry point for sequential extraction.
/// </summary>
public sealed class SevenZipReader
    : AbstractReader<SevenZipEntry, SevenZipVolume>,
        ISevenZipReader,
        ISevenZipAsyncReader
{
    private readonly SevenZipArchive _archive;
    private readonly bool _disposeArchive;
    private SevenZipEntry? _currentEntry;
    private Stream? _currentFolderStream;
    private CFolder? _currentFolder;

    /// <summary>
    /// Enables internal diagnostics for tests.
    /// When disabled (default), diagnostics properties return null to avoid exposing internal state.
    /// </summary>
    internal bool DiagnosticsEnabled { get; set; }

    /// <summary>
    /// Current folder instance used to decide whether the solid folder stream should be reused.
    /// Only available when <see cref="DiagnosticsEnabled"/> is true.
    /// </summary>
    internal object? DiagnosticsCurrentFolder => DiagnosticsEnabled ? _currentFolder : null;

    /// <summary>
    /// Current shared folder stream instance.
    /// Only available when <see cref="DiagnosticsEnabled"/> is true.
    /// </summary>
    internal Stream? DiagnosticsCurrentFolderStream =>
        DiagnosticsEnabled ? _currentFolderStream : null;
    internal SevenZipReader(
        ReaderOptions readerOptions,
        SevenZipArchive archive,
        bool disposeArchive = false
    )
        : base(readerOptions, ArchiveType.SevenZip, disposeVolume: false)
    {
        _archive = archive;
        _disposeArchive = disposeArchive;
    }

    /// <summary>
    /// Opens a 7Zip reader from a file path.
    /// </summary>
    public static ISevenZipReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    /// <summary>
    /// Opens a 7Zip reader from a file.
    /// </summary>
    public static ISevenZipReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        var options = readerOptions ?? ReaderOptions.ForOwnedFile;
        return OpenReader(fileInfo.OpenRead(), options);
    }

    /// <summary>
    /// Opens a 7Zip reader from a stream.
    /// </summary>
    public static ISevenZipReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        return new SevenZipReader(
            options,
            (SevenZipArchive)SevenZipArchive.OpenArchive(stream, options),
            disposeArchive: true
        );
    }

    /// <summary>
    /// Opens a 7Zip reader from a file path asynchronously.
    /// </summary>
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncReader(new FileInfo(filePath), readerOptions, cancellationToken);
    }

    /// <summary>
    /// Opens a 7Zip reader from a file asynchronously.
    /// </summary>
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsyncReader(
            fileInfo.OpenRead(),
            readerOptions ?? ReaderOptions.ForOwnedFile,
            cancellationToken
        );
    }

    /// <summary>
    /// Opens a 7Zip reader from a stream asynchronously.
    /// </summary>
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)OpenReader(stream, readerOptions));
    }

    public override SevenZipVolume Volume => _archive.Volumes.Single();

    protected override IEnumerable<SevenZipEntry> GetEntries(Stream stream)
    {
        var entries = _archive.Entries.ToList();
        stream.Position = 0;
        foreach (var dir in entries.Where(x => x.IsDirectory))
        {
            _currentEntry = dir;
            yield return dir;
        }
        // For solid archives (entries in the same folder share a compressed stream),
        // we must iterate entries sequentially and maintain the folder stream state
        // across entries in the same folder to avoid recreating the decompression
        // stream for each file, which breaks contiguous streaming.
        foreach (var entry in entries.Where(x => !x.IsDirectory))
        {
            _currentEntry = entry;
            yield return entry;
        }
    }

    protected override EntryStream GetEntryStream()
    {
        var entry = _currentEntry.NotNull("currentEntry is not null");
        if (entry.IsDirectory)
        {
            return CreateEntryStream(Stream.Null);
        }

        var folder = entry.FilePart.Folder;

        // If folder is null (empty stream entry), return empty stream
        if (folder is null)
        {
            return CreateEntryStream(Stream.Null);
        }

        // Check if we're starting a new folder - dispose old folder stream if needed
        if (folder != _currentFolder)
        {
            _currentFolderStream?.Dispose();
            _currentFolderStream = null;
            _currentFolder = folder;
        }

        // Create the folder stream once per folder
        if (_currentFolderStream is null)
        {
            var database = _archive.Database.NotNull("database is not loaded");
            _currentFolderStream = database.GetFolderStream(
                _archive.Volumes.Single().Stream,
                folder,
                database.PasswordProvider
            );
        }

        return CreateEntryStream(
            new ReadOnlySubStream(_currentFolderStream, entry.Size, leaveOpen: true)
        );
    }

    protected override ValueTask<EntryStream> GetEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) => new(GetEntryStream());

    public override void Dispose()
    {
        _currentFolderStream?.Dispose();
        _currentFolderStream = null;
        base.Dispose();
        if (_disposeArchive)
        {
            _archive.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _currentFolderStream?.Dispose();
        _currentFolderStream = null;
        await base.DisposeAsync().ConfigureAwait(false);
        if (_disposeArchive)
        {
            await _archive.DisposeAsync().ConfigureAwait(false);
        }
    }
}
