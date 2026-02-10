using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;

namespace SharpCompress.Readers.Tar;

public partial class TarReader
{
    /// <summary>
    /// Returns entries asynchronously for streams that only support async reads.
    /// Uses async decompression for compressed tar archives (gzip, bzip2, zstandard, etc.).
    /// </summary>
    protected override IAsyncEnumerable<TarEntry> GetEntriesAsync(Stream stream) =>
        TarEntry.GetEntriesAsync(
            StreamingMode.Streaming,
            stream,
            compressionType,
            Options.ArchiveEncoding,
            Options
        );
}
