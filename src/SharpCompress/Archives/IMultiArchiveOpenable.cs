#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public interface IMultiArchiveOpenable<TSync, TASync>
    where TSync : IArchive
    where TASync : IAsyncArchive
{
    public static abstract TSync Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    );

    public static abstract TSync Open(
        IEnumerable<Stream> streams,
        ReaderOptions? readerOptions = null
    );

    public static abstract TASync OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract TASync OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );
}
#endif
