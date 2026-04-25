using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        readerOptions ??= ReaderOptions.ForExternalStream;
        var factory = await FindFactoryAsync<IArchiveFactory>(stream, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncArchive(
            new FileInfo(filePath),
            options ?? ReaderOptions.ForFilePath,
            cancellationToken
        );
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ReaderOptions.ForFilePath;

        var factory = await FindFactoryAsync<IArchiveFactory>(fileInfo, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(fileInfo, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var filesArray = fileInfos;
        if (filesArray.Count == 0)
        {
            throw new ArchiveOperationException("No files to open");
        }

        var fileInfo = filesArray[0];
        if (filesArray.Count == 1)
        {
            return await OpenAsyncArchive(fileInfo, options, cancellationToken)
                .ConfigureAwait(false);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= ReaderOptions.ForFilePath;

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(fileInfo, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(filesArray, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var streamsArray = streams.RequireReadable().RequireSeekable().ToList();
        var firstStream = streamsArray[0];
        if (streamsArray.Count == 1)
        {
            return await OpenAsyncArchive(firstStream, options, cancellationToken)
                .ConfigureAwait(false);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= ReaderOptions.ForExternalStream;

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(firstStream, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(streamsArray, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns information about the archive at the given file path asynchronously,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await GetArchiveInformationAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns information about the archive in the given stream asynchronously,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            var isArchive = await factory
                .IsArchiveAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            stream.Position = startPosition;

            if (isArchive)
            {
                return new ArchiveInformation(factory.KnownArchiveType, factory is IArchiveFactory);
            }
        }

        return null;
    }

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return FindFactoryAsync<T>(new FileInfo(filePath), cancellationToken);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo finfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factories = Factory.Factories.OfType<T>();

        var startPosition = stream.Position;

        foreach (var factory in factories)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);

            if (
                await factory
                    .IsArchiveAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                stream.Seek(startPosition, SeekOrigin.Begin);

                return factory;
            }
        }

        var extensions = string.Join(", ", factories.Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }
}
