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

/// <summary>
/// Opens independent archive, reader, and writer instances using client-local dependencies.
/// The caller owns returned instances. A client can be reused concurrently; opened instances cannot.
/// </summary>
public interface ISharpCompressClient
{
    IArchiveInspector Inspector { get; }
    IArchiveExtractor Extractor { get; }
    IArchiveFileWriter FileWriter { get; }

    IArchive OpenArchive(Stream stream, ReaderOptions? options = null);
    IArchive OpenArchive(string filePath, ReaderOptions? options = null);
    IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null);
    IArchive OpenArchive(IReadOnlyList<Stream> streams, ReaderOptions? options = null);
    IArchive OpenArchive(IReadOnlyList<FileInfo> files, ReaderOptions? options = null);
    ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncArchive> OpenAsyncArchive(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> files,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions;

    IReader OpenReader(Stream stream, ReaderOptions? options = null);
    IReader OpenReader(string filePath, ReaderOptions? options = null);
    IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null);
    ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );

    IWriter OpenWriter(Stream stream, ArchiveType archiveType, IWriterOptions options);
    IWriter OpenWriter(string filePath, ArchiveType archiveType, IWriterOptions options);
    IWriter OpenWriter(FileInfo fileInfo, ArchiveType archiveType, IWriterOptions options);
    ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    );
    ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions options,
        CancellationToken cancellationToken = default
    );
}
