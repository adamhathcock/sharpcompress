using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public static class WriterFactory
{
    public static IWriter OpenWriter(
        string filePath,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), archiveType, writerOptions);
    }

    public static IWriter OpenWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenWriter(fileInfo.OpenWrite(), archiveType, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        string filePath,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncWriter(new FileInfo(filePath), archiveType, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenAsyncWriter(
            fileInfo.Open(FileMode.Create, FileAccess.Write),
            archiveType,
            writerOptions
        );
    }

    public static IWriter OpenWriter(
        Stream stream,
        ArchiveType archiveType,
        WriterOptions writerOptions
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
    /// <returns>A task that returns an IWriter.</returns>
    public static IAsyncWriter OpenAsyncWriter(
        Stream stream,
        ArchiveType archiveType,
        WriterOptions writerOptions
    )
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return factory.OpenAsyncWriter(stream, writerOptions);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
