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
        return OpenAsyncArchive(new FileInfo(filePath), options, cancellationToken);
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
        streams.NotNull(nameof(streams));
        var streamsArray = streams;
        if (streamsArray.Count == 0)
        {
            throw new ArchiveOperationException("No streams");
        }

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

    public static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return FindFactoryAsync<T>(new FileInfo(filePath), cancellationToken);
    }

    private static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo finfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        stream.NotNull(nameof(stream));
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Stream should be readable and seekable");
        }

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
