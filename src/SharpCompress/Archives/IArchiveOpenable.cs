#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public interface IArchiveOpenable<TSync, TASync>
    where TSync : IArchive
    where TASync : IAsyncArchive
{
    public static abstract TSync Open(string filePath, ReaderOptions? readerOptions = null);

    public static abstract TSync Open(FileInfo fileInfo, ReaderOptions? readerOptions = null);

    public static abstract TSync Open(Stream stream, ReaderOptions? readerOptions = null);

    public static abstract TASync OpenAsync(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract TASync OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );

    public static abstract TASync OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    );
}
#endif
