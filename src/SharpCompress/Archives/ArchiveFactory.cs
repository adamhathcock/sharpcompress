using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        return FactoryDetection
            .FindFactory<IArchiveFactory>(stream)
            .OpenArchive(stream, readerOptions);
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

    public static IArchive OpenArchive(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenArchive(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public static IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= ReaderOptions.ForFilePath;

        return FactoryDetection
            .FindFactory<IArchiveFactory>(fileInfo)
            .OpenArchive(fileInfo, options);
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

        return FactoryDetection
            .FindFactory<IMultiArchiveFactory>(fileInfo)
            .OpenArchive(filesArray, options);
    }

    public static IArchive OpenArchive(IReadOnlyList<Stream> streams, ReaderOptions? options = null)
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

        return FactoryDetection
            .FindFactory<IMultiArchiveFactory>(firstStream)
            .OpenArchive(streamsArray, options);
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
        return FactoryDetection.FindFactory<T>(filePath);
    }

    public static T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        return FactoryDetection.FindFactory<T>(finfo);
    }

    public static T FindFactory<T>(Stream stream)
        where T : IFactory
    {
        return FactoryDetection.FindFactory<T>(stream);
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
