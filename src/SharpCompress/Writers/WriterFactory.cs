using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Services;

namespace SharpCompress.Writers;

public static class WriterFactory
{
    public static IWriter OpenWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => LibraryDefaults.Writers.OpenWriter(filePath, archiveType, writerOptions);

    public static IWriter OpenWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => LibraryDefaults.Writers.OpenWriter(fileInfo, archiveType, writerOptions);

    public static IWriter OpenWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => LibraryDefaults.Writers.OpenWriter(stream, archiveType, writerOptions);

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    ) =>
        LibraryDefaults.Writers.OpenAsyncWriter(
            filePath,
            archiveType,
            writerOptions,
            cancellationToken
        );

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    ) =>
        LibraryDefaults.Writers.OpenAsyncWriter(
            fileInfo,
            archiveType,
            writerOptions,
            cancellationToken
        );

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    ) =>
        LibraryDefaults.Writers.OpenAsyncWriter(
            stream,
            archiveType,
            writerOptions,
            cancellationToken
        );
}
