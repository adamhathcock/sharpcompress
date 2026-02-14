using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

public partial class SevenZipArchive : AbstractArchive<SevenZipArchiveEntry, SevenZipVolume>
{
    private ArchiveDatabase? _database;

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    private SevenZipArchive(SourceStream sourceStream)
        : base(ArchiveType.SevenZip, sourceStream) { }

    protected override IEnumerable<SevenZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts(); //request all streams
        return new SevenZipVolume(sourceStream, ReaderOptions, 0).AsEnumerable(); //simple single volume or split, multivolume not supported
    }

    internal SevenZipArchive()
        : base(ArchiveType.SevenZip) { }

    protected override IEnumerable<SevenZipArchiveEntry> LoadEntries(
        IEnumerable<SevenZipVolume> volumes
    )
    {
        foreach (var volume in volumes)
        {
            LoadFactory(volume.Stream);
            if (_database is null)
            {
                yield break;
            }
            var entries = new SevenZipArchiveEntry[_database._files.Count];
            for (var i = 0; i < _database._files.Count; i++)
            {
                var file = _database._files[i];
                entries[i] = new SevenZipArchiveEntry(
                    this,
                    new SevenZipFilePart(
                        volume.Stream,
                        _database,
                        i,
                        file,
                        ReaderOptions.ArchiveEncoding
                    ),
                    ReaderOptions
                );
            }
            foreach (
                var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder)
            )
            {
                var isSolid = false;
                foreach (var entry in group)
                {
                    entry.IsSolid = isSolid;
                    isSolid = true;
                }
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }

    private void LoadFactory(Stream stream)
    {
        if (_database is null)
        {
            stream.Position = 0;
            var reader = new ArchiveReader();
            reader.Open(stream, lookForHeader: ReaderOptions.LookForHeader);
            _database = reader.ReadDatabase(new PasswordProvider(ReaderOptions.Password));
        }
    }

    protected override IReader CreateReaderForSolidExtraction() =>
        new SevenZipReader(ReaderOptions, this);

    public override bool IsSolid =>
        Entries
            .Where(x => !x.IsDirectory)
            .GroupBy(x => x.FilePart.Folder)
            .Any(folder => folder.Count() > 1);

    public override bool IsEncrypted => Entries.First(x => !x.IsDirectory).IsEncrypted;

    public override long TotalSize =>
        _database?._packSizes.Aggregate(0L, (total, packSize) => total + packSize) ?? 0;

    internal sealed class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
    {
        private readonly SevenZipArchive _archive;
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

        internal SevenZipReader(ReaderOptions readerOptions, SevenZipArchive archive)
            : base(readerOptions, ArchiveType.SevenZip, false) => this._archive = archive;

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
                _currentFolderStream = _archive._database!.GetFolderStream(
                    _archive.Volumes.Single().Stream,
                    folder!,
                    _archive._database.PasswordProvider
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
        }
    }

    /// <summary>
    /// WORKAROUND: Forces async operations to use synchronous equivalents.
    /// This is necessary because the LZMA decoder has bugs in its async implementation
    /// that cause state corruption (IndexOutOfRangeException, DataErrorException).
    ///
    /// The proper fix would be to repair the LZMA decoder's async methods
    /// (LzmaStream.ReadAsync, Decoder.CodeAsync, OutWindow async operations),
    /// but that requires deep changes to the decoder state machine.
    /// </summary>
    private sealed class SyncOnlyStream : Stream
    {
        private readonly Stream _baseStream;

        public SyncOnlyStream(Stream baseStream) => _baseStream = baseStream;

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _baseStream.Write(buffer, offset, count);

        // Force async operations to use sync equivalents to avoid LZMA decoder bugs
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_baseStream.Read(buffer, offset, count));
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            _baseStream.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _baseStream.Flush();
            return Task.CompletedTask;
        }

#if !LEGACY_DOTNET
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(_baseStream.Read(buffer.Span));
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            _baseStream.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private class PasswordProvider : IPasswordProvider
    {
        private readonly string? _password;

        public PasswordProvider(string? password) => _password = password;

        public string? CryptoGetTextPassword() => _password;
    }
}
