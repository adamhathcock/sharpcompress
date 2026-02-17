using System.Collections.Generic;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Archive that supports extracting individual entries via OpenEntryStream.
/// </summary>
public interface IExtractableArchive : IArchive
{
    /// <summary>
    /// Entries that support opening a decompressed content stream directly.
    /// </summary>
    new IEnumerable<IExtractableArchiveEntry> Entries { get; }
}

/// <summary>
/// Async archive that supports extracting individual entries via OpenEntryStream.
/// </summary>
public interface IExtractableAsyncArchive : IAsyncArchive
{
    /// <summary>
    /// Entries that support opening a decompressed content stream directly.
    /// </summary>
    new IAsyncEnumerable<IExtractableArchiveEntry> EntriesAsync { get; }
}
