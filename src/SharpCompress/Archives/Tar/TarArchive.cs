using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

public partial class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
{
    protected override IEnumerable<TarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.NotNull("SourceStream is null").LoadAllParts();
        return new TarVolume(sourceStream, ReaderOptions, 1).AsEnumerable();
    }

    private TarArchive(SourceStream sourceStream)
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
                            entryStream.CopyTo(memoryStream);
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

    // Async iterator method - kept in original file (cannot be split with partial classes)
    protected override async IAsyncEnumerable<TarArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<TarVolume> volumes
    )
    {
        var stream = (await volumes.SingleAsync()).Stream;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // Always use async header reading in LoadEntriesAsync for consistency
        {
            // Use async header reading for async-only streams
            TarHeader? previousHeader = null;
            await foreach (
                var header in TarHeaderFactory.ReadHeaderAsync(
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
                                await entryStream.CopyToAsync(memoryStream);
                                memoryStream.Position = 0;
                                var bytes = memoryStream.ToArray();

                                header.Name = ReaderOptions
                                    .ArchiveEncoding.Decode(bytes)
                                    .TrimNulls();
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
        WriterOptions options,
        IEnumerable<TarArchiveEntry> oldEntries,
        IEnumerable<TarArchiveEntry> newEntries
    )
    {
        using var writer = new TarWriter(stream, new TarWriterOptions(options));
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
        return TarReader.OpenReader(stream);
    }
}
