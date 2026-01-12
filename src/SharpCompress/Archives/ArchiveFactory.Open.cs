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
    public static IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= new ReaderOptions();
        stream = SharpCompressStream.Create(stream, bufferSize: readerOptions.BufferSize);
        return FindFactory<IArchiveFactory>(stream).Open(stream, readerOptions);
    }

    public static async ValueTask<IAsyncArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        readerOptions ??= new ReaderOptions();
        stream = SharpCompressStream.Create(stream, bufferSize: readerOptions.BufferSize);
        var factory = await FindFactoryAsync<IArchiveFactory>(stream, cancellationToken);
        return factory.OpenAsync(stream, readerOptions);
    }

    public static IWritableArchive Create(ArchiveType type)
    {
        var factory = Factory
            .Factories.OfType<IWriteableArchiveFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == type);

        if (factory != null)
        {
            return factory.CreateWriteableArchive();
        }

        throw new NotSupportedException("Cannot create Archives of type: " + type);
    }

    public static IArchive Open(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), options);
    }

    public static ValueTask<IAsyncArchive> OpenAsync(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsync(new FileInfo(filePath), options, cancellationToken);
    }

    public static IArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        return FindFactory<IArchiveFactory>(fileInfo).Open(fileInfo, options);
    }

    public static async ValueTask<IAsyncArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        var factory = await FindFactoryAsync<IArchiveFactory>(fileInfo, cancellationToken);
        return factory.OpenAsync(fileInfo, options, cancellationToken);
    }

    public static IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? options = null)
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
            return Open(fileInfo, options);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        return FindFactory<IMultiArchiveFactory>(fileInfo).Open(filesArray, options);
    }

    public static async ValueTask<IAsyncArchive> OpenAsync(
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
            return await OpenAsync(fileInfo, options, cancellationToken);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(fileInfo, cancellationToken);
        return factory.OpenAsync(filesArray, options, cancellationToken);
    }

    public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        var streamsArray = streams.ToArray();
        if (streamsArray.Length == 0)
        {
            throw new InvalidOperationException("No streams");
        }

        var firstStream = streamsArray[0];
        if (streamsArray.Length == 1)
        {
            return Open(firstStream, options);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= new ReaderOptions();

        return FindFactory<IMultiArchiveFactory>(firstStream).Open(streamsArray, options);
    }

    public static async ValueTask<IAsyncArchive> OpenAsync(
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
            return await OpenAsync(firstStream, options, cancellationToken);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= new ReaderOptions();

        var factory = FindFactory<IMultiArchiveFactory>(firstStream);
        return factory.OpenAsync(streamsArray, options);
    }

    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var archive = Open(sourceArchive);
        archive.WriteToDirectory(destinationDirectory, options);
    }

    private static T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream);
    }

    private static T FindFactory<T>(Stream stream)
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

            if (factory.IsArchive(stream))
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

    public static bool IsArchive(
        string filePath,
        out ArchiveType? type,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream s = File.OpenRead(filePath);
        return IsArchive(s, out type, bufferSize);
    }

    public static bool IsArchive(
        Stream stream,
        out ArchiveType? type,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        type = null;
        stream.NotNull(nameof(stream));

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Stream should be readable and seekable");
        }

        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            var isArchive = factory.IsArchive(stream);
            stream.Position = startPosition;

            if (isArchive)
            {
                type = factory.KnownArchiveType;
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> GetFileParts(string part1)
    {
        part1.NotNullOrEmpty(nameof(part1));
        return GetFileParts(new FileInfo(part1)).Select(a => a.FullName);
    }

    public static IEnumerable<FileInfo> GetFileParts(FileInfo part1)
    {
        part1.NotNull(nameof(part1));
        yield return part1;

        foreach (var factory in Factory.Factories.OfType<IFactory>())
        {
            var i = 1;
            var part = factory.GetFilePart(i++, part1);

            if (part != null)
            {
                yield return part;
                while ((part = factory.GetFilePart(i++, part1)) != null)
                {
                    yield return part;
                }

                yield break;
            }
        }
    }

    public static IArchiveFactory AutoFactory { get; } = new AutoArchiveFactory();
}
