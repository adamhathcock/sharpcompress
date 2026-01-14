using System;
using System.IO;
using System.Linq;
using System.Threading;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public static class WriterFactory
{
    public static IWriter Open(
        string filePath,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), archiveType, writerOptions);
    }

    public static IWriter Open(
        FileInfo fileInfo,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return Open(fileInfo.OpenWrite(), archiveType, writerOptions);
    }

    public static IAsyncWriter OpenAsync(
        string filePath,
        ArchiveType archiveType,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsync(new FileInfo(filePath), archiveType, writerOptions, cancellationToken);
    }

    public static IAsyncWriter OpenAsync(
        FileInfo fileInfo,
        ArchiveType archiveType,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsync(
            fileInfo.Open(FileMode.Create, FileAccess.Write),
            archiveType,
            writerOptions,
            cancellationToken
        );
    }

    public static IWriter Open(Stream stream, ArchiveType archiveType, WriterOptions writerOptions)
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return factory.Open(stream, writerOptions);
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
    /// <returns>A task that returns an IWriter.</returns>
    public static IAsyncWriter OpenAsync(
        Stream stream,
        ArchiveType archiveType,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return factory.OpenAsync(stream, writerOptions, cancellationToken);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
