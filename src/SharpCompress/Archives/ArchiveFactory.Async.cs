using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;
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
        readerOptions ??= new ReaderOptions();
        stream = SharpCompressStream.Create(stream, bufferSize: readerOptions.BufferSize);
        var factory = await FindFactoryAsync<IArchiveFactory>(stream, cancellationToken);
        return factory.OpenAsyncArchive(stream, readerOptions);
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
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        var factory = await FindFactoryAsync<IArchiveFactory>(fileInfo, cancellationToken);
        return factory.OpenAsyncArchive(fileInfo, options);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var filesArray = fileInfos.ToArray();
        if (filesArray.Length == 0)
        {
            throw new InvalidOperationException("No files to open");
        }

        var fileInfo = filesArray[0];
        if (filesArray.Length == 1)
        {
            return await OpenAsyncArchive(fileInfo, options, cancellationToken);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(fileInfo, cancellationToken);
        return factory.OpenAsyncArchive(filesArray, options, cancellationToken);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IEnumerable<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        streams.NotNull(nameof(streams));
        var streamsArray = streams.ToArray();
        if (streamsArray.Length == 0)
        {
            throw new InvalidOperationException("No streams");
        }

        var firstStream = streamsArray[0];
        if (streamsArray.Length == 1)
        {
            return await OpenAsyncArchive(firstStream, options, cancellationToken);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= new ReaderOptions();

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(firstStream, cancellationToken);
        return factory.OpenAsyncArchive(streamsArray, options);
    }

    public static ValueTask<T> FindFactoryAsync<T>(
        string path,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        path.NotNullOrEmpty(nameof(path));
        return FindFactoryAsync<T>(new FileInfo(path), cancellationToken);
    }

    private static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo finfo,
        CancellationToken cancellationToken
    )
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken);
    }

    private static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken
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

            if (await factory.IsArchiveAsync(stream, cancellationToken: cancellationToken))
            {
                stream.Seek(startPosition, SeekOrigin.Begin);

                return factory;
            }
        }

        var extensions = string.Join(", ", factories.Select(item => item.Name));

        throw new InvalidOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }
}
