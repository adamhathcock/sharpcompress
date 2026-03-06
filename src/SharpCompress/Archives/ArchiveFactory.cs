using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    public static IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= ReaderOptions.ForExternalStream;
        return FindFactory<IArchiveFactory>(stream).OpenArchive(stream, readerOptions);
    }

    public static IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions
    {
        var factory = Factory
            .Factories.OfType<IWritableArchiveFactory<TOptions>>()
            .FirstOrDefault();

        if (factory != null)
        {
            return factory.CreateArchive();
        }

        throw new NotSupportedException("Cannot create Archives of type: " + typeof(TOptions));
    }

    public static ValueTask<IWritableAsyncArchive<TOptions>> CreateAsyncArchive<TOptions>(
        CancellationToken cancellationToken = default
    )
        where TOptions : IWriterOptions
    {
        cancellationToken.ThrowIfCancellationRequested();
        var factory = Factory
            .Factories.OfType<IWritableAsyncArchiveFactory<TOptions>>()
            .FirstOrDefault();

        if (factory is not null)
        {
            return factory.CreateAsyncArchive(cancellationToken);
        }

        throw new NotSupportedException("Cannot create Archives of type: " + typeof(TOptions));
    }

    public static IArchive OpenArchive(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), options);
    }

    public static IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= ReaderOptions.ForFilePath;

        return FindFactory<IArchiveFactory>(fileInfo).OpenArchive(fileInfo, options);
    }

    public static IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? options = null
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
            return OpenArchive(fileInfo, options);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= ReaderOptions.ForFilePath;

        return FindFactory<IMultiArchiveFactory>(fileInfo).OpenArchive(filesArray, options);
    }

    public static IArchive OpenArchive(IReadOnlyList<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        var streamsArray = streams;
        if (streamsArray.Count == 0)
        {
            throw new ArchiveOperationException("No streams");
        }

        var firstStream = streamsArray[0];
        if (streamsArray.Count == 1)
        {
            return OpenArchive(firstStream, options);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= ReaderOptions.ForExternalStream;

        return FindFactory<IMultiArchiveFactory>(firstStream).OpenArchive(streamsArray, options);
    }

    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var archive = OpenArchive(sourceArchive);
        archive.WriteToDirectory(destinationDirectory, options);
    }

    public static T FindFactory<T>(string filePath)
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return FindFactory<T>(stream);
    }

    public static T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream);
    }

    public static T FindFactory<T>(Stream stream)
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

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }

    public static bool IsArchive(string filePath, out ArchiveType? type)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream s = File.OpenRead(filePath);
        return IsArchive(s, out type);
    }

    public static bool IsArchive(Stream stream, out ArchiveType? type)
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

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return IsArchiveAsync(stream, cancellationToken);
    }

    public static async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Stream should be readable and seekable");
        }

        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            var isArchive = await factory
                .IsArchiveAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            stream.Position = startPosition;

            if (isArchive)
            {
                return (true, factory.KnownArchiveType);
            }
        }

        return (false, null);
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
}
