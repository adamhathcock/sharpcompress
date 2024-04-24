using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
{
    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static TarArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return new TarArchive(
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
    public static TarArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new TarArchive(
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
    public static TarArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.CheckNotNull(nameof(streams));
        var strms = streams.ToArray();
        return new TarArchive(
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
    public static TarArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new TarArchive(
            new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static bool IsTarFile(string filePath) => IsTarFile(new FileInfo(filePath));

    public static bool IsTarFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsTarFile(stream);
    }

    public static bool IsTarFile(Stream stream)
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            var readSucceeded = tarHeader.Read(new BinaryReader(stream));
            var isEmptyArchive =
                tarHeader.Name?.Length == 0
                && tarHeader.Size == 0
                && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
            return readSucceeded || isEmptyArchive;
        }
        catch { }
        return false;
    }

    protected override IEnumerable<TarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts(); //request all streams
        return new TarVolume(sourceStream, ReaderOptions, 1).AsEnumerable(); //simple single volume or split, multivolume not supported
    }

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    private TarArchive(SourceStream sourceStream)
        : base(ArchiveType.Tar, sourceStream) { }

    private TarArchive()
        : base(ArchiveType.Tar) { }

    protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        TarHeader? previousHeader = null;
        foreach (
            var header in TarHeaderFactory.ReadHeader(
                StreamingMode.Seekable,
                stream,
                ReaderOptions.ArchiveEncoding
            )
        )
        {
            if (header != null)
            {
                if (header.EntryType == EntryType.LongName)
                {
                    previousHeader = header;
                }
                else
                {
                    if (previousHeader != null)
                    {
                        var entry = new TarArchiveEntry(
                            this,
                            new TarFilePart(previousHeader, stream),
                            CompressionType.None
                        );

                        var oldStreamPos = stream.Position;

                        using (var entryStream = entry.OpenEntryStream())
                        {
                            using var memoryStream = new MemoryStream();
                            entryStream.TransferTo(memoryStream);
                            memoryStream.Position = 0;
                            var bytes = memoryStream.ToArray();

                            header.Name = ReaderOptions.ArchiveEncoding.Decode(bytes).TrimNulls();
                        }

                        stream.Position = oldStreamPos;

                        previousHeader = null;
                    }
                    yield return new TarArchiveEntry(
                        this,
                        new TarFilePart(header, stream),
                        CompressionType.None
                    );
                }
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
    }

    public static TarArchive Create() => new();

    protected override TarArchiveEntry CreateEntryInternal(
        string filePath,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    ) =>
        new TarWritableArchiveEntry(
            this,
            source,
            CompressionType.Unknown,
            filePath,
            size,
            modified,
            closeStream
        );

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries
    )
    {
        using var writer = new TarWriter(stream, new TarWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries).Where(x => !x.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            writer.Write(
                entry.Key.NotNull("Entry Key is null"),
                entryStream,
                entry.LastModifiedTime,
                entry.Size
            );
        }
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return TarReader.Open(stream);
    }
}
