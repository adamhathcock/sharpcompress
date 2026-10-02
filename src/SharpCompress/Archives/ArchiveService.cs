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

internal sealed partial class ArchiveService(SharpCompressConfiguration configuration)
    : IArchiveInspector
{
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions = configuration.PrepareReaderOptions(readerOptions, true);
        return FindFactory<IArchiveFactory>(stream, readerOptions)
            .OpenArchive(stream, readerOptions);
    }

    public IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions
    {
        var factory = configuration
            .Formats.Factories.OfType<IWritableArchiveFactory<TOptions>>()
            .FirstOrDefault();

        if (factory != null)
        {
            var archive = factory.CreateArchive();
            archive.ReaderOptions.Providers = configuration.Providers;
            archive.ReaderOptions.Formats = configuration.Formats;
            return archive;
        }

        throw new NotSupportedException("Cannot create Archives of type: " + typeof(TOptions));
    }

    public IArchive OpenArchive(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options = configuration.PrepareReaderOptions(options, false);

        return FindFactory<IArchiveFactory>(fileInfo, options).OpenArchive(fileInfo, options);
    }

    public IArchive OpenArchive(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? options = null)
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
        options = configuration.PrepareReaderOptions(options, false);

        return FindFactory<IMultiArchiveFactory>(fileInfo, options)
            .OpenArchive(filesArray, options);
    }

    public IArchive OpenArchive(IReadOnlyList<Stream> streams, ReaderOptions? options = null)
    {
        var streamsArray = streams.RequireReadable().RequireSeekable().ToList();
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
        options = configuration.PrepareReaderOptions(options, true);

        return FindFactory<IMultiArchiveFactory>(firstStream, options)
            .OpenArchive(streamsArray, options);
    }

    public T FindFactory<T>(string filePath)
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return FindFactory<T>(stream);
    }

    public T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream);
    }

    public T FindFactory<T>(Stream stream)
        where T : IFactory => FindFactory<T>(stream, ReaderOptions.ForExternalStream);

    public bool IsArchive(string filePath, out ArchiveType? type)
    {
        return IsArchive(filePath, ReaderOptions.ForFilePath, out type);
    }

    public bool IsArchive(string filePath, ReaderOptions? readerOptions, out ArchiveType? type)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream s = File.OpenRead(filePath);
        return IsArchive(s, readerOptions ?? ReaderOptions.ForFilePath, out type);
    }

    public bool IsArchive(Stream stream, out ArchiveType? type)
    {
        return IsArchive(stream, ReaderOptions.ForExternalStream, out type);
    }

    public bool IsArchive(Stream stream, ReaderOptions? readerOptions, out ArchiveType? type)
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = TryFindFactory(
            stream,
            configuration.PrepareReaderOptions(readerOptions, true)
        );
        type = factory?.KnownArchiveType;
        return factory is not null;
    }

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await IsArchiveAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = await TryFindFactoryAsync(
                stream,
                configuration.PrepareReaderOptions(readerOptions, true),
                cancellationToken
            )
            .ConfigureAwait(false);
        return (factory is not null, factory?.KnownArchiveType);
    }

    public IEnumerable<string> GetFileParts(string part1)
    {
        part1.NotNullOrEmpty(nameof(part1));
        return GetFileParts(new FileInfo(part1)).Select(a => a.FullName);
    }

    public IEnumerable<FileInfo> GetFileParts(FileInfo part1)
    {
        part1.NotNull(nameof(part1));
        yield return part1;

        foreach (var factory in configuration.Formats.Factories)
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
