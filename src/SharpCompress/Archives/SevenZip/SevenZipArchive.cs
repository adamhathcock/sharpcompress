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

    private SevenZipArchive(SourceStream sourceStream)
        : base(ArchiveType.SevenZip, sourceStream) { }

    internal SevenZipArchive()
        : base(ArchiveType.SevenZip) { }

    protected override IEnumerable<SevenZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts();
        return new SevenZipVolume(sourceStream, ReaderOptions, 0).AsEnumerable();
    }

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
                    )
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
            return CreateEntryStream(new SyncOnlyStream(entry.FilePart.GetCompressedStream()));
        }
    }

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
