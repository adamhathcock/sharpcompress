using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;
using CompressionMode = SharpCompress.Compressors.CompressionMode;
using Constants = SharpCompress.Common.Constants;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive
    : AbstractWritableArchive<TarArchiveEntry, TarVolume, TarWriterOptions>
{
    private readonly CompressionType _compressionType;

    protected override IEnumerable<TarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts();
        return new TarVolume(sourceStream, ReaderOptions, 1).AsEnumerable();
    }

    internal TarArchive(SourceStream sourceStream, CompressionType compressionType)
        : base(ArchiveType.Tar, sourceStream)
    {
        _compressionType = compressionType;
    }

    private TarArchive()
        : base(ArchiveType.Tar) { }

    private Stream GetStream(Stream stream) =>
        _compressionType switch
        {
            CompressionType.BZip2 => BZip2Stream.Create(stream, CompressionMode.Decompress, false),
            CompressionType.GZip => new GZipStream(stream, CompressionMode.Decompress),
            CompressionType.ZStandard => new ZStandardStream(stream),
            CompressionType.LZip => new LZipStream(stream, CompressionMode.Decompress),
            CompressionType.Xz => new XZStream(stream),
            CompressionType.Lzw => new LzwStream(stream),
            CompressionType.None => stream,
            _ => throw new NotSupportedException("Invalid compression type: " + _compressionType),
        };

    protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
    {
        var stream = GetStream(volumes.Single().Stream);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        TarHeader? previousHeader = null;
        foreach (
            var header in TarHeaderFactory.ReadHeader(
                _compressionType == CompressionType.None
                    ? StreamingMode.Seekable
                    : StreamingMode.Streaming,
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
                            CompressionType.None,
                            ReaderOptions
                        );

                        var oldStreamPos = stream.Position;

                        using (var entryStream = entry.OpenEntryStream())
                        {
                            using var memoryStream = new MemoryStream();
                            entryStream.CopyTo(memoryStream, Constants.BufferSize);
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
                        CompressionType.None,
                        ReaderOptions
                    );
                }
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
    }

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

    protected override TarArchiveEntry CreateDirectoryEntry(
        string directoryPath,
        DateTime? modified
    ) => new TarWritableArchiveEntry(this, directoryPath, modified);

    protected override void SaveTo(
        Stream stream,
        TarWriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries
    )
    {
        using var writer = new TarWriter(
            stream,
            options as TarWriterOptions ?? new TarWriterOptions(options)
        );
        foreach (var entry in oldEntries.Concat(newEntries))
        {
            if (entry.IsDirectory)
            {
                writer.WriteDirectory(
                    entry.Key.NotNull("Entry Key is null"),
                    entry.LastModifiedTime
                );
            }
            else
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
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return TarReader.OpenReader(GetStream(stream));
    }
}
