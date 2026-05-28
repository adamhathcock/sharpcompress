using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.GZip;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader
{
    /// <summary>
    /// Returns entries asynchronously for streams that only support async reads.
    /// </summary>
    protected override IAsyncEnumerable<GZipEntry> GetEntriesAsync(Stream stream) =>
        GZipEntry.GetEntriesAsync(stream, Options);
}
