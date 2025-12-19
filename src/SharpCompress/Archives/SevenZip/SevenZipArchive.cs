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

public class SevenZipArchive : AbstractArchive<SevenZipArchiveEntry, SevenZipVolume>
{
    private ArchiveDatabase? _database;

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty("filePath");
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull("fileInfo");
        return new SevenZipArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    )
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull("stream");

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new SevenZipArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

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

    public static bool IsSevenZipFile(string filePath) => IsSevenZipFile(new FileInfo(filePath));

    public static bool IsSevenZipFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsSevenZipFile(stream);
    }

    internal SevenZipArchive()
        : base(ArchiveType.SevenZip) { }

    protected override IEnumerable<SevenZipArchiveEntry> LoadEntries(
        IEnumerable<SevenZipVolume> volumes
    )
    {
        var stream = volumes.Single().Stream;
        LoadFactory(stream);
        if (_database is null)
        {
            return Enumerable.Empty<SevenZipArchiveEntry>();
        }
        var entries = new SevenZipArchiveEntry[_database._files.Count];
        for (var i = 0; i < _database._files.Count; i++)
        {
            var file = _database._files[i];
            entries[i] = new SevenZipArchiveEntry(
                this,
                new SevenZipFilePart(stream, _database, i, file, ReaderOptions.ArchiveEncoding)
            );
        }
        foreach (var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder))
        {
            var isSolid = false;
            foreach (var entry in group)
            {
                entry.IsSolid = isSolid;
                isSolid = true; //mark others in this group as solid - same as rar behaviour.
            }
        }

        return entries;
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

    public static bool IsSevenZipFile(Stream stream)
    {
        try
        {
            return SignatureMatch(stream);
        }
        catch
        {
            return false;
        }
    }

    private static ReadOnlySpan<byte> Signature =>
        new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

    private static bool SignatureMatch(Stream stream)
    {
        var reader = new BinaryReader(stream);
        ReadOnlySpan<byte> signatureBytes = reader.ReadBytes(6);
        return signatureBytes.SequenceEqual(Signature);
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

    private sealed class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
    {
        private readonly SevenZipArchive _archive;
        private SevenZipEntry? _currentEntry;

        internal SevenZipReader(ReaderOptions readerOptions, SevenZipArchive archive)
            : base(readerOptions, ArchiveType.SevenZip) => this._archive = archive;

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
            // For non-directory entries, yield them without creating shared streams
            // Each call to GetEntryStream() will create a fresh decompression stream
            // to avoid state corruption issues with async operations
            foreach (var entry in entries.Where(x => !x.IsDirectory))
            {
                _currentEntry = entry;
                yield return entry;
            }
        }

        protected override EntryStream GetEntryStream()
        {
            // Create a fresh decompression stream for each file (no state sharing).
            // However, the LZMA decoder has bugs in its async implementation that cause
            // state corruption even on fresh streams. The SyncOnlyStream wrapper
            // works around these bugs by forcing async operations to use sync equivalents.
            //
            // TODO: Fix the LZMA decoder async bugs (in LzmaStream, Decoder, OutWindow)
            // so this wrapper is no longer necessary.
            var entry = _currentEntry.NotNull("currentEntry is not null");
            if (entry.IsDirectory)
            {
                return CreateEntryStream(Stream.Null);
            }
            return CreateEntryStream(new SyncOnlyStream(entry.FilePart.GetCompressedStream()));
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

#if !NETFRAMEWORK && !NETSTANDARD2_0
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
