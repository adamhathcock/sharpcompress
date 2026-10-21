using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Writers;

namespace SharpCompress.Services;

internal sealed class WriterService(FactoryCatalog catalog)
{
    public IWriter OpenWriter(
        string filePath,
        ArchiveType archiveType,
        IWriterOptions writerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), archiveType, writerOptions);
    }

    public IWriter OpenWriter(
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

    public async ValueTask<IAsyncWriter> OpenAsyncWriter(
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
            )
            .ConfigureAwait(false);
    }

    public async ValueTask<IAsyncWriter> OpenAsyncWriter(
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
            )
            .ConfigureAwait(false);
    }

    public IWriter OpenWriter(Stream stream, ArchiveType archiveType, IWriterOptions writerOptions)
    {
        stream.RequireWritable();

        var factory = catalog
            .Factories.OfType<IWriterFactory>()
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
    public async ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireWritable();

        var factory = catalog
            .Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return await factory
                .OpenAsyncWriter(stream, writerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
