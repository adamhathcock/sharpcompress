using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        filePath.CheckNotNullOrEmpty("filePath");
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull("fileInfo");
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
        fileInfos.CheckNotNull(nameof(fileInfos));
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
        streams.CheckNotNull(nameof(streams));
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
        stream.CheckNotNull("stream");
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
        Entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder).Count() > 1;

    public override long TotalSize =>
        _database?._packSizes.Aggregate(0L, (total, packSize) => total + packSize) ?? 0;

    private sealed class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
    {
        private readonly SevenZipArchive _archive;
        private CFolder? _currentFolder;
        private Stream? _currentStream;
        private CFileItem? _currentItem;

        internal SevenZipReader(ReaderOptions readerOptions, SevenZipArchive archive)
            : base(readerOptions, ArchiveType.SevenZip) => this._archive = archive;

        public override SevenZipVolume Volume => _archive.Volumes.Single();

        protected override IEnumerable<SevenZipEntry> GetEntries(Stream stream)
        {
            var entries = _archive.Entries.ToList();
            stream.Position = 0;
            foreach (var dir in entries.Where(x => x.IsDirectory))
            {
                yield return dir;
            }
            foreach (
                var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder)
            )
            {
                _currentFolder = group.Key;
                if (group.Key is null)
                {
                    _currentStream = Stream.Null;
                }
                else
                {
                    _currentStream = _archive._database?.GetFolderStream(
                        stream,
                        _currentFolder,
                        new PasswordProvider(Options.Password)
                    );
                }
                foreach (var entry in group)
                {
                    _currentItem = entry.FilePart.Header;
                    yield return entry;
                }
            }
        }

        protected override EntryStream GetEntryStream() =>
            CreateEntryStream(
                new ReadOnlySubStream(
                    _currentStream.NotNull("currentStream is not null"),
                    _currentItem?.Size ?? 0
                )
            );
    }

    private class PasswordProvider : IPasswordProvider
    {
        private readonly string? _password;

        public PasswordProvider(string? password) => _password = password;

        public string? CryptoGetTextPassword() => _password;
    }
}
