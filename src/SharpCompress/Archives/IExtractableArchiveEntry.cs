using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Archives;

/// <summary>
/// Archive entry that supports opening a decompressed content stream directly.
/// </summary>
public interface IExtractableArchiveEntry : IArchiveEntry
{
    /// <summary>
    /// Opens the current entry as a stream that will decompress as it is read.
    /// Read the entire stream or use SkipEntry on EntryStream.
    /// </summary>
    Stream OpenEntryStream();

    /// <summary>
    /// Opens the current entry as a stream that will decompress as it is read asynchronously.
    /// Read the entire stream or use SkipEntry on EntryStream.
    /// </summary>
    ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default);
}
