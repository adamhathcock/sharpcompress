using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress;

/// <summary>A reusable client with immutable configuration and independent workflow services.</summary>
public sealed class SharpCompressClient : ISharpCompressClient
{
    internal ClientServices Services { get; }
    public SharpCompressConfiguration Configuration { get; }
    public IArchiveInspector Inspector => Services.Archives;
    public IArchiveExtractor Extractor => Services.Extractor;
    public IArchiveFileWriter FileWriter => Services.FileWriter;

    /// <summary>Creates a client. Omitted configuration uses the built-in formats and providers.</summary>
    public SharpCompressClient(SharpCompressConfiguration? configuration = null)
    {
        Configuration = configuration ?? new SharpCompressConfiguration();
        // Pure.DI only constructs stateless services. Opened archives and streams are caller-owned.
        Services = new ServiceComposition(Configuration).Services;
    }

    public IArchive OpenArchive(Stream stream, ReaderOptions? options = null) =>
        Services.Archives.OpenArchive(stream, options);

    public IArchive OpenArchive(string filePath, ReaderOptions? options = null) =>
        Services.Archives.OpenArchive(filePath, options);

    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null) =>
        Services.Archives.OpenArchive(fileInfo, options);

    public IArchive OpenArchive(IReadOnlyList<Stream> streams, ReaderOptions? options = null) =>
        Services.Archives.OpenArchive(streams, options);

    public IArchive OpenArchive(IReadOnlyList<FileInfo> files, ReaderOptions? options = null) =>
        Services.Archives.OpenArchive(files, options);

    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Archives.OpenAsyncArchive(stream, options, cancellationToken);

    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Archives.OpenAsyncArchive(filePath, options, cancellationToken);

    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Archives.OpenAsyncArchive(fileInfo, options, cancellationToken);

    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Archives.OpenAsyncArchive(streams, options, cancellationToken);

    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> files,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Archives.OpenAsyncArchive(files, options, cancellationToken);

    public IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions => Services.Archives.CreateArchive<TOptions>();

    public IReader OpenReader(Stream stream, ReaderOptions? options = null) =>
        Services.Readers.OpenReader(stream, options);

    public IReader OpenReader(string filePath, ReaderOptions? options = null) =>
        Services.Readers.OpenReader(filePath, options);

    public IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null) =>
        Services.Readers.OpenReader(fileInfo, options);

    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Readers.OpenAsyncReader(stream, options, cancellationToken);

    public ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Readers.OpenAsyncReader(filePath, options, cancellationToken);

    public ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Services.Readers.OpenAsyncReader(fileInfo, options, cancellationToken);

    public IWriter OpenWriter(Stream stream, ArchiveType archiveType, IWriterOptions options) =>
        Services.Writers.OpenWriter(stream, archiveType, options);

    public IWriter OpenWriter(string filePath, ArchiveType archiveType, IWriterOptions options) =>
        Services.Writers.OpenWriter(filePath, archiveType, options);

    public IWriter OpenWriter(FileInfo fileInfo, ArchiveType archiveType, IWriterOptions options) =>
        Services.Writers.OpenWriter(fileInfo, archiveType, options);

    public ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    ) => Services.Writers.OpenAsyncWriter(stream, archiveType, options, cancellationToken);

    public ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    ) => Services.Writers.OpenAsyncWriter(filePath, archiveType, options, cancellationToken);

    public ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    ) => Services.Writers.OpenAsyncWriter(fileInfo, archiveType, options, cancellationToken);
}
