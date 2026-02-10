#if NET8_0_OR_GREATER
using System.IO;
using System.Threading;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public interface IArchiveOpenable<TSync, TASync>
    where TSync : IArchive
    where TASync : IAsyncArchive
{
    public static abstract TSync OpenArchive(string filePath, ReaderOptions? readerOptions = null);

    public static abstract TSync OpenArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    );

    public static abstract TSync OpenArchive(Stream stream, ReaderOptions? readerOptions = null);

    public static abstract TASync OpenAsyncArchive(
        string path,
        ReaderOptions? readerOptions = null
    );

    public static abstract TASync OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null
    );

    public static abstract TASync OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    );
}

#endif
