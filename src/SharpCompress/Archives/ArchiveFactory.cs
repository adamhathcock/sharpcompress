using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class ArchiveFactory
{
    /// <summary>
    /// Opens an Archive for random access
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= new ReaderOptions();

        return FindFactory<IArchiveFactory>(stream).Open(stream, readerOptions);
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

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    public static IArchive Open(string filePath, ReaderOptions? options = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), options);
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="options"></param>
    public static IArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        return FindFactory<IArchiveFactory>(fileInfo).Open(fileInfo, options);
    }

    /// <summary>
    /// Constructor with IEnumerable FileInfo objects, multi and split support.
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="options"></param>
    public static IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? options = null)
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
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

        fileInfo.CheckNotNull(nameof(fileInfo));
        options ??= new ReaderOptions { LeaveStreamOpen = false };

        return FindFactory<IMultiArchiveFactory>(fileInfo).Open(filesArray, options);
    }

    /// <summary>
    /// Constructor with IEnumerable FileInfo objects, multi and split support.
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
    public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.CheckNotNull(nameof(streams));
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

        firstStream.CheckNotNull(nameof(firstStream));
        options ??= new ReaderOptions();

        return FindFactory<IMultiArchiveFactory>(firstStream).Open(streamsArray, options);
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var archive = Open(sourceArchive);
        foreach (var entry in archive.Entries)
        {
            entry.WriteToDirectory(destinationDirectory, options);
        }
    }

    private static T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.CheckNotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream);
    }

    private static T FindFactory<T>(Stream stream)
        where T : IFactory
    {
        stream.CheckNotNull(nameof(stream));
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

    public static bool IsArchive(string filePath, out ArchiveType? type)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        using Stream s = File.OpenRead(filePath);
        return IsArchive(s, out type);
    }

    public static bool IsArchive(Stream stream, out ArchiveType? type)
    {
        type = null;
        stream.CheckNotNull(nameof(stream));

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

    /// <summary>
    /// From a passed in archive (zip, rar, 7z, 001), return all parts.
    /// </summary>
    /// <param name="part1"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetFileParts(string part1)
    {
        part1.CheckNotNullOrEmpty(nameof(part1));
        return GetFileParts(new FileInfo(part1)).Select(a => a.FullName);
    }

    /// <summary>
    /// From a passed in archive (zip, rar, 7z, 001), return all parts.
    /// </summary>
    /// <param name="part1"></param>
    /// <returns></returns>
    public static IEnumerable<FileInfo> GetFileParts(FileInfo part1)
    {
        part1.CheckNotNull(nameof(part1));
        yield return part1;

        foreach (var factory in Factory.Factories.OfType<IFactory>())
        {
            var i = 1;
            var part = factory.GetFilePart(i++, part1);

            if (part != null)
            {
                yield return part;
                while ((part = factory.GetFilePart(i++, part1)) != null) //tests split too
                {
                    yield return part;
                }

                yield break;
            }
        }
    }

    public static IArchiveFactory AutoFactory { get; } = new AutoArchiveFactory();
}
