using System.Collections.Generic;
using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.GZip;

public partial class GZipEntry
{
    internal static async IAsyncEnumerable<GZipEntry> GetEntriesAsync(
        Stream stream,
        ReaderOptions options
    )
    {
        yield return new GZipEntry(
            await GZipFilePart
                .CreateAsync(stream, options.ArchiveEncoding, options.CompressionProviders)
                .ConfigureAwait(false),
            options
        );
    }
}
