using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pure.DI;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed partial class ArchiveService(IFactoryCatalog catalog, IArchiveDetector detector)
    : IArchiveService
{
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= ReaderOptions.ForExternalStream;
        return detector
            .FindFactory<IArchiveFactory>(stream, readerOptions)
            .OpenArchive(stream, readerOptions);
    }

    public IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions
    {
        var factory = catalog
            .Factories.OfType<IWritableArchiveFactory<TOptions>>()
            .FirstOrDefault();

        if (factory != null)
        {
            return factory.CreateArchive();
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
        options ??= ReaderOptions.ForFilePath;

        return detector
            .FindFactory<IArchiveFactory>(fileInfo, options)
            .OpenArchive(fileInfo, options);
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
        options ??= ReaderOptions.ForFilePath;

        return detector
            .FindFactory<IMultiArchiveFactory>(fileInfo, options)
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
        options ??= ReaderOptions.ForExternalStream;

        return detector
            .FindFactory<IMultiArchiveFactory>(firstStream, options)
            .OpenArchive(streamsArray, options);
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

        foreach (var factory in catalog.Factories.OfType<IFactory>())
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
