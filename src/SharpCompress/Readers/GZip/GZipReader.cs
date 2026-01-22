using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.GZip;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader : AbstractReader<GZipEntry, GZipVolume>
{
    private GZipReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.GZip) => Volume = new GZipVolume(stream, options, 0);

    public override GZipVolume Volume { get; }

    protected override IEnumerable<GZipEntry> GetEntries(Stream stream) =>
        GZipEntry.GetEntries(stream, Options);

    // GetEntriesAsync moved to GZipReader.Async.cs
}
