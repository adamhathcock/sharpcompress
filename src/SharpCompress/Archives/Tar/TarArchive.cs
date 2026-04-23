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
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;
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
            CompressionType.BZip2 => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.BZip2,
                stream
            ),
            CompressionType.GZip => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.GZip,
                stream,
                CompressionContext.FromStream(stream).WithReaderOptions(ReaderOptions)
            ),
            CompressionType.ZStandard => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.ZStandard,
                stream
            ),
            CompressionType.LZip => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.LZip,
                stream
            ),
            CompressionType.Xz => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.Xz,
                stream
            ),
            CompressionType.Lzw => ReaderOptions.Providers.CreateDecompressStream(
                CompressionType.Lzw,
                stream
            ),
            CompressionType.None => stream,
            _ => throw new NotSupportedException("Invalid compression type: " + _compressionType),
        };

    private ValueTask<Stream> GetStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        _compressionType switch
        {
            CompressionType.BZip2 => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.BZip2,
                stream,
                cancellationToken
            ),
            CompressionType.GZip => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.GZip,
                stream,
                CompressionContext.FromStream(stream).WithReaderOptions(ReaderOptions),
                cancellationToken
            ),
            CompressionType.ZStandard => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.ZStandard,
                stream,
                cancellationToken
            ),
            CompressionType.LZip => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.LZip,
                stream,
                cancellationToken
            ),
            CompressionType.Xz => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.Xz,
                stream,
                cancellationToken
            ),
            CompressionType.Lzw => ReaderOptions.Providers.CreateDecompressStreamAsync(
                CompressionType.Lzw,
                stream,
                cancellationToken
            ),
            CompressionType.None => new ValueTask<Stream>(stream),
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
        string? previousLongName = null;
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
                    if (_compressionType != CompressionType.None)
                    {
                        // Streaming (compressed) mode: read the long name immediately
                        // from the PackedStream before the foreach loop advances and
                        // ReadHeader disposes it while skipping to the next header.
                        using var longNameStream = header.PackedStream.NotNull();
                        using var memoryStream = new MemoryStream();
                        longNameStream.CopyTo(memoryStream, Constants.BufferSize);
                        previousLongName = ReaderOptions
                            .ArchiveEncoding.Decode(memoryStream.ToArray())
                            .TrimNulls();
                    }
                    else
                    {
                        // Seekable (uncompressed) mode: save the header; the long name
                        // will be read after the next entry header has been parsed.
                        previousHeader = header;
                    }
                }
                else
                {
                    if (previousLongName != null)
                    {
                        // Apply the long name that was pre-read in streaming mode.
                        header.Name = previousLongName;
                        previousLongName = null;
                    }
                    else if (previousHeader != null)
                    {
                        // Seekable mode: read the long name via the seekable stream now.
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
                        new TarFilePart(
                            header,
                            _compressionType == CompressionType.None ? stream : null
                        ),
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
        return new TarReader(stream, ReaderOptions, _compressionType);
    }
}
