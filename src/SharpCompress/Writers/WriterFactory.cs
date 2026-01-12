using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public static class WriterFactory
{
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
    public static async ValueTask<IWriter> OpenAsync(
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
            return await factory
                .OpenAsync(stream, writerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
