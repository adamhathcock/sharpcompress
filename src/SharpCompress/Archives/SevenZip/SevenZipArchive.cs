#nullable disable

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
    private ArchiveDatabase database;

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(string filePath, ReaderOptions readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty("filePath");
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static SevenZipArchive Open(FileInfo fileInfo, ReaderOptions readerOptions = null)
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
        ReaderOptions readerOptions = null
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
        ReaderOptions readerOptions = null
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
    public static SevenZipArchive Open(Stream stream, ReaderOptions readerOptions = null)
    {
        stream.CheckNotNull("stream");
        return new SevenZipArchive(
            new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions())
        );
    }

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="srcStream"></param>
    /// <param name="options"></param>
    internal SevenZipArchive(SourceStream srcStream) : base(ArchiveType.SevenZip, srcStream) { }

    protected override IEnumerable<SevenZipVolume> LoadVolumes(SourceStream srcStream)
    {
        SrcStream.LoadAllParts(); //request all streams
        var idx = 0;
        return new SevenZipVolume(srcStream, ReaderOptions, idx++).AsEnumerable(); //simple single volume or split, multivolume not supported
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

    internal SevenZipArchive() : base(ArchiveType.SevenZip) { }

    protected override IEnumerable<SevenZipArchiveEntry> LoadEntries(
        IEnumerable<SevenZipVolume> volumes
    )
    {
        var stream = volumes.Single().Stream;
        LoadFactory(stream);
        var entries = new SevenZipArchiveEntry[database._files.Count];
        for (var i = 0; i < database._files.Count; i++)
        {
            var file = database._files[i];
            entries[i] = new SevenZipArchiveEntry(
                this,
                new SevenZipFilePart(stream, database, i, file, ReaderOptions.ArchiveEncoding)
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
        if (database is null)
        {
            stream.Position = 0;
            var reader = new ArchiveReader();
            reader.Open(stream);
            database = reader.ReadDatabase(new PasswordProvider(ReaderOptions.Password));
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

    private static ReadOnlySpan<byte> SIGNATURE =>
        new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

    private static bool SignatureMatch(Stream stream)
    {
        var reader = new BinaryReader(stream);
        ReadOnlySpan<byte> signatureBytes = reader.ReadBytes(6);
        return signatureBytes.SequenceEqual(SIGNATURE);
    }

    protected override IReader CreateReaderForSolidExtraction() =>
        new SevenZipReader(ReaderOptions, this);

    public override bool IsSolid =>
        Entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder).Count() > 1;

    public override long TotalSize
    {
        get
        {
            var i = Entries.Count;
            return database._packSizes.Aggregate(0L, (total, packSize) => total + packSize);
        }
    }

    private sealed class SevenZipReader : AbstractReader<SevenZipEntry, SevenZipVolume>
    {
        private readonly SevenZipArchive archive;
        private CFolder currentFolder;
        private Stream currentStream;
        private CFileItem currentItem;

        internal SevenZipReader(ReaderOptions readerOptions, SevenZipArchive archive)
            : base(readerOptions, ArchiveType.SevenZip) => this.archive = archive;

        public override SevenZipVolume Volume => archive.Volumes.Single();

        protected override IEnumerable<SevenZipEntry> GetEntries(Stream stream)
        {
            var entries = archive.Entries.ToList();
            stream.Position = 0;
            foreach (var dir in entries.Where(x => x.IsDirectory))
            {
                yield return dir;
            }
            foreach (
                var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder)
            )
            {
                currentFolder = group.Key;
                if (group.Key is null)
                {
                    currentStream = Stream.Null;
                }
                else
                {
                    currentStream = archive.database.GetFolderStream(
                        stream,
                        currentFolder,
                        new PasswordProvider(Options.Password)
                    );
                }
                foreach (var entry in group)
                {
                    currentItem = entry.FilePart.Header;
                    yield return entry;
                }
            }
        }

        protected override EntryStream GetEntryStream() =>
            CreateEntryStream(new ReadOnlySubStream(currentStream, currentItem.Size));
    }

    private class PasswordProvider : IPasswordProvider
    {
        private readonly string _password;

        public PasswordProvider(string password) => _password = password;

        public string CryptoGetTextPassword() => _password;
    }
}
