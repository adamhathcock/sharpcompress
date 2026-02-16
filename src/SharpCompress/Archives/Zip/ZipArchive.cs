using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchive
    : AbstractWritableArchive<ZipArchiveEntry, ZipVolume, ZipWriterOptions>
{
    private readonly SeekableZipHeaderFactory? headerFactory;

    public CompressionLevel DeflateCompressionLevel { get; set; }

    internal ZipArchive(SourceStream sourceStream)
        : base(ArchiveType.Zip, sourceStream) =>
        headerFactory = new SeekableZipHeaderFactory(
            sourceStream.ReaderOptions.Password,
            sourceStream.ReaderOptions.ArchiveEncoding
        );

    internal ZipArchive()
        : base(ArchiveType.Zip) { }

    protected override IEnumerable<ZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        //stream.Position = 0;

        var streams = sourceStream.Streams.ToList();
        var idx = 0;
        if (streams.Count > 1)
        {
            //check if second stream is zip header without changing position
            var headerProbeStream = streams[1];
            var startPosition = headerProbeStream.Position;
            headerProbeStream.Position = startPosition + 4;
            var isZip = IsZipFile(headerProbeStream, ReaderOptions.Password);
            headerProbeStream.Position = startPosition;
            if (isZip)
            {
                sourceStream.IsVolumes = true;

                var tmp = streams[0];
                streams.RemoveAt(0);
                streams.Add(tmp);

                return streams.Select(a => new ZipVolume(a, ReaderOptions, idx++));
            }
        }

        return new ZipVolume(sourceStream, ReaderOptions, idx++).AsEnumerable();
    }

    protected override IEnumerable<ZipArchiveEntry> LoadEntries(IEnumerable<ZipVolume> volumes)
    {
        var vols = volumes.ToArray();
        foreach (var h in headerFactory.NotNull().ReadSeekableHeader(vols.Last().Stream))
        {
            if (h != null)
            {
                switch (h.ZipHeaderType)
                {
                    case ZipHeaderType.DirectoryEntry:
                        {
                            var deh = (DirectoryEntryHeader)h;
                            Stream s;
                            if (
                                deh.RelativeOffsetOfEntryHeader + deh.CompressedSize
                                > vols[deh.DiskNumberStart].Stream.Length
                            )
                            {
                                var v = vols.Skip(deh.DiskNumberStart).ToArray();
                                s = new SourceStream(
                                    v[0].Stream,
                                    i => i < v.Length ? v[i].Stream : null,
                                    new ReaderOptions() { LeaveStreamOpen = true }
                                );
                            }
                            else
                            {
                                s = vols[deh.DiskNumberStart].Stream;
                            }

                            yield return new ZipArchiveEntry(
                                this,
                                new SeekableZipFilePart(
                                    headerFactory.NotNull(),
                                    deh,
                                    s,
                                    ReaderOptions.Providers
                                ),
                                ReaderOptions
                            );
                        }
                        break;
                    case ZipHeaderType.DirectoryEnd:
                    {
                        var bytes = ((DirectoryEndHeader)h).Comment ?? Array.Empty<byte>();
                        vols.Last().Comment = ReaderOptions.ArchiveEncoding.Decode(bytes);
                        yield break;
                    }
                }
            }
        }
    }

    public void SaveTo(Stream stream) =>
        SaveTo(stream, new ZipWriterOptions(CompressionType.Deflate));

    protected override void SaveTo(
        Stream stream,
        ZipWriterOptions options,
        IEnumerable<ZipArchiveEntry> oldEntries,
        IEnumerable<ZipArchiveEntry> newEntries
    )
    {
        using var writer = new ZipWriter(stream, options);
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
                    entry.LastModifiedTime
                );
            }
        }
    }

    protected override ZipArchiveEntry CreateEntryInternal(
        string key,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    ) => new ZipWritableArchiveEntry(this, source, key, size, modified, closeStream);

    protected override ZipArchiveEntry CreateDirectoryEntry(string key, DateTime? modified) =>
        new ZipWritableArchiveEntry(this, key, modified);

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        //stream.Position = 0;
        return ZipReader.OpenReader(stream, ReaderOptions, Entries);
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return new((IAsyncReader)ZipReader.OpenReader(stream, ReaderOptions, Entries));
    }
}
