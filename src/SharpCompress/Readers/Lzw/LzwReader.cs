using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Lzw;

namespace SharpCompress.Readers.Lzw;

public partial class LzwReader : AbstractReader<LzwEntry, LzwVolume>
{
    private LzwReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Lzw) => Volume = new LzwVolume(stream, options, 0);

    public override LzwVolume Volume { get; }

    protected override IEnumerable<LzwEntry> GetEntries(Stream stream) =>
        LzwEntry.GetEntries(stream, Options);

    // GetEntriesAsync moved to LzwReader.Async.cs
}
