using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Common.Options;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

public partial class GZipArchive
    : AbstractWritableArchive<GZipArchiveEntry, GZipVolume, GZipWriterOptions>
{
    private GZipArchive(SourceStream sourceStream)
        : base(ArchiveType.GZip, sourceStream) { }

    internal GZipArchive()
        : base(ArchiveType.GZip) { }

    protected override IEnumerable<GZipVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        return sourceStream.Streams.Select(a => new GZipVolume(a, ReaderOptions, 0));
    }

    protected override GZipArchiveEntry CreateEntryInternal(
        string key,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    )
    {
        if (Entries.Any())
        {
            throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
        }
        return new GZipWritableArchiveEntry(this, source, key, size, modified, closeStream);
    }

    protected override GZipArchiveEntry CreateDirectoryEntry(string key, DateTime? modified) =>
        throw new NotSupportedException("GZip archives do not support directory entries.");

    protected override IEnumerable<GZipArchiveEntry> LoadEntries(IEnumerable<GZipVolume> volumes)
    {
        // Kept hand-written: the generator cannot map IAsyncEnumerable.SingleAsync to LINQ Single.
        var stream = volumes.Single().Stream;
        yield return new GZipArchiveEntry(
            this,
            GZipFilePart.Create(stream, ReaderOptions.ArchiveEncoding, ReaderOptions.Providers),
            ReaderOptions
        );
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return GZipReader.OpenReader(stream, ReaderOptions);
    }
}
