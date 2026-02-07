using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SharpCompress.Common.Lzw;

public partial class LzwEntry
{
    internal static async IAsyncEnumerable<LzwEntry> GetEntriesAsync(
        Stream stream,
        OptionsBase options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return new LzwEntry(
            await LzwFilePart.CreateAsync(stream, options.ArchiveEncoding, cancellationToken)
        );
    }
}
