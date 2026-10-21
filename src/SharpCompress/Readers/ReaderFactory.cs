using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Services;

namespace SharpCompress.Readers;

public static partial class ReaderFactory
{
    public static IReader OpenReader(string filePath, ReaderOptions? options = null) =>
        LibraryDefaults.Composition.Readers.OpenReader(filePath, options);

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null) =>
        LibraryDefaults.Composition.Readers.OpenReader(fileInfo, options);

    /// <summary>Open a reader for forward-only usage.</summary>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null) =>
        LibraryDefaults.Composition.Readers.OpenReader(stream, options);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => LibraryDefaults.Composition.Readers.OpenAsyncReader(filePath, options, cancellationToken);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => LibraryDefaults.Composition.Readers.OpenAsyncReader(fileInfo, options, cancellationToken);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => LibraryDefaults.Composition.Readers.OpenAsyncReader(stream, options, cancellationToken);
}
