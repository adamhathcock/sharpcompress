using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive
    : AbstractWritableArchive<TarArchiveEntry, TarVolume, TarWriterOptions>
{
    protected override IEnumerable<TarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts();
        return new TarVolume(sourceStream, ReaderOptions, 1).AsEnumerable();
    }

    internal TarArchive(SourceStream sourceStream)
        : base(ArchiveType.Tar, sourceStream) { }

    private TarArchive()
        : base(ArchiveType.Tar) { }

    protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
    {
        var stream = volumes.Single().Stream;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
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
                yield return new TarArchiveEntry(
                    this,
                    new TarFilePart(header, stream),
                    CompressionType.None,
                    ReaderOptions
                );
            }
            else
            {
                throw new IncompleteArchiveException("Failed to read TAR header");
            }
        }
    }

    protected override TarArchiveEntry CreateEntryInternal(
        string key,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    ) =>
        new TarWritableArchiveEntry(
            this,
            source,
            CompressionType.Unknown,
            key,
            size,
            modified,
            closeStream
        );

    protected override TarArchiveEntry CreateDirectoryEntry(string key, DateTime? modified) =>
        new TarWritableArchiveEntry(this, key, modified);

    protected override void SaveTo(
        Stream stream,
        TarWriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries
    )
    {
        using var writer = new TarWriter(stream, options);
        foreach (var entry in oldEntries.Concat(newEntries))
        {
            if (entry.IsDirectory)
            {
                writer.WriteDirectory(
                    entry.Key.NotNull("Entry Key is null"),
                    entry.LastModifiedTime,
                    entry.Metadata
                );
            }
            else
            {
                using var entryStream = entry.OpenEntryStream();
                writer.Write(
                    entry.Key.NotNull("Entry Key is null"),
                    entryStream,
                    entry.LastModifiedTime,
                    entry.Size,
                    entry.Metadata
                );
            }
        }
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return new TarReader(stream, ReaderOptions, CompressionType.None);
    }
}
