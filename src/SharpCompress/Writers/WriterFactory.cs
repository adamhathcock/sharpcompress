using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace SharpCompress.Writers;

public static class WriterFactory
{
    public static IWriter OpenWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), archiveType, writerOptions);
    }

    public static IWriter OpenWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenWriter(
            fileInfo.OpenWrite(),
            archiveType,
            writerOptions.WithLeaveStreamOpen(false)
        );
    }

    public static async ValueTask<IAsyncWriter> OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return await OpenAsyncWriter(
            new FileInfo(filePath),
            archiveType,
            writerOptions,
            cancellationToken
        );
    }

    public static async ValueTask<IAsyncWriter> OpenAsyncWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        var stream = fileInfo.OpenAsyncWriteStream(cancellationToken);
        return await OpenAsyncWriter(
            stream,
            archiveType,
            writerOptions.WithLeaveStreamOpen(false),
            cancellationToken
        );
    }

    public static IWriter OpenWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    )
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return factory.OpenWriter(stream, writerOptions);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }

    /// <summary>
    /// Opens a Writer asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="archiveType">The archive type.</param>
    /// <param name="writerOptions">Writer options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> containing the async writer.</returns>
    public static async ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return await factory.OpenAsyncWriter(stream, writerOptions, cancellationToken);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
