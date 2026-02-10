using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Lzw;

namespace SharpCompress.Readers.Lzw;

public partial class LzwReader
{
    /// <summary>
    /// Returns entries asynchronously for streams that only support async reads.
    /// </summary>
    protected override IAsyncEnumerable<LzwEntry> GetEntriesAsync(Stream stream) =>
        LzwEntry.GetEntriesAsync(stream, Options);
}
