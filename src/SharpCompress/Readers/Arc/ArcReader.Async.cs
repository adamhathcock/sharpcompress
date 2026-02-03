using System.Collections.Generic;
using System.IO;
using System.Threading;
using SharpCompress.Common.Arc;

namespace SharpCompress.Readers.Arc
{
    public partial class ArcReader
    {
        protected override async IAsyncEnumerable<ArcEntry> GetEntriesAsync(Stream stream)
        {
            ArcEntryHeader headerReader = new ArcEntryHeader(Options.ArchiveEncoding);
            ArcEntryHeader? header;
            while (
                (header = await headerReader.ReadHeaderAsync(stream, CancellationToken.None))
                != null
            )
            {
                yield return new ArcEntry(new ArcFilePart(header, stream));
            }
        }
    }
}
