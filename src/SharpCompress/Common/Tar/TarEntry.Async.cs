using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

public partial class TarEntry
{
    internal static async IAsyncEnumerable<TarEntry> GetEntriesAsync(
        StreamingMode mode,
        Stream stream,
        CompressionType compressionType,
        IArchiveEncoding archiveEncoding
    )
    {
        await foreach (
            var header in TarHeaderFactory.ReadHeaderAsync(mode, stream, archiveEncoding)
        )
        {
            if (header != null)
            {
                if (mode == StreamingMode.Seekable)
                {
                    yield return new TarEntry(new TarFilePart(header, stream), compressionType);
                }
                else
                {
                    yield return new TarEntry(new TarFilePart(header, null), compressionType);
                }
            }
            else
            {
                throw new IncompleteArchiveException("Unexpected EOF reading tar file");
            }
        }
    }
}
