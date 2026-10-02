using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers;

/// <summary>Convenience reader opening using the immutable default client.</summary>
public static class ReaderFactory
{
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null) =>
        ClientDefaults.Client.OpenReader(stream, options);

    public static IReader OpenReader(string filePath, ReaderOptions? options = null) =>
        ClientDefaults.Client.OpenReader(filePath, options);

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null) =>
        ClientDefaults.Client.OpenReader(fileInfo, options);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => ClientDefaults.Client.OpenAsyncReader(stream, options, cancellationToken);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => ClientDefaults.Client.OpenAsyncReader(filePath, options, cancellationToken);

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => ClientDefaults.Client.OpenAsyncReader(fileInfo, options, cancellationToken);
}
