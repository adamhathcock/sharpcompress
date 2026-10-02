using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace SharpCompress.Writers;

/// <summary>Convenience writer opening using the immutable default client.</summary>
public static class WriterFactory
{
    public static IWriter OpenWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => ClientDefaults.Client.OpenWriter(stream, archiveType, writerOptions);

    public static IWriter OpenWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => ClientDefaults.Client.OpenWriter(filePath, archiveType, writerOptions);

    public static IWriter OpenWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    ) => ClientDefaults.Client.OpenWriter(fileInfo, archiveType, writerOptions);

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    ) =>
        ClientDefaults.Client.OpenAsyncWriter(
            stream,
            archiveType,
            writerOptions,
            cancellationToken
        );

    public static ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    ) =>
        ClientDefaults.Client.OpenAsyncWriter(
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
        ClientDefaults.Client.OpenAsyncWriter(
            fileInfo,
            archiveType,
            writerOptions,
            cancellationToken
        );
}
