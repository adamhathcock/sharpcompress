using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.GZip;

public partial class GZipEntry
{
    internal static async IAsyncEnumerable<GZipEntry> GetEntriesAsync(
        Stream stream,
        OptionsBase options
    )
    {
        yield return new GZipEntry(await GZipFilePart.CreateAsync(stream, options.ArchiveEncoding));
    }
}
