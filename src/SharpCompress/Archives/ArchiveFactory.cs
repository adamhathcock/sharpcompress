using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Factories;
using SharpCompress.IO;
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
        stream = SharpCompressStream.Create(stream, bufferSize: readerOptions.BufferSize);
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
        filePath.NotNullOrEmpty(nameof(filePath));
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

    /// <summary>
    /// Constructor with IEnumerable FileInfo objects, multi and split support.
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
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
        archive.WriteToDirectory(destinationDirectory, options);
    }

    private static T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream, finfo.Name);
    }

    private static T FindFactory<T>(Stream stream, string? fileName = null)
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

        stream.Seek(startPosition, SeekOrigin.Begin);

        // Check if this is a compressed tar file (tar.bz2, tar.lz, etc.)
        // These formats are supported by ReaderFactory but not by ArchiveFactory
        var compressedTarMessage = TryGetCompressedTarMessage(stream, fileName);
        if (compressedTarMessage != null)
        {
            throw new InvalidOperationException(compressedTarMessage);
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

    /// <summary>
    /// From a passed in archive (zip, rar, 7z, 001), return all parts.
    /// </summary>
    /// <param name="part1"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetFileParts(string part1)
    {
        part1.NotNullOrEmpty(nameof(part1));
        return GetFileParts(new FileInfo(part1)).Select(a => a.FullName);
    }

    /// <summary>
    /// From a passed in archive (zip, rar, 7z, 001), return all parts.
    /// </summary>
    /// <param name="part1"></param>
    /// <returns></returns>
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
                while ((part = factory.GetFilePart(i++, part1)) != null) //tests split too
                {
                    yield return part;
                }

                yield break;
            }
        }
    }

    public static IArchiveFactory AutoFactory { get; } = new AutoArchiveFactory();

    /// <summary>
    /// Checks if the stream is a compressed tar file (tar.bz2, tar.lz, etc.) that should use ReaderFactory instead.
    /// Returns an error message if detected, null otherwise.
    /// </summary>
    private static string? TryGetCompressedTarMessage(Stream stream, string? fileName)
    {
        var startPosition = stream.Position;
        try
        {
            // Check if it's a BZip2 file
            if (BZip2Stream.IsBZip2(stream))
            {
                stream.Seek(startPosition, SeekOrigin.Begin);

                // Try to decompress and check if it contains a tar archive
                using var decompressed = new BZip2Stream(stream, CompressionMode.Decompress, true);
                if (IsTarStream(decompressed))
                {
                    return "This appears to be a tar.bz2 archive. Compressed tar formats (tar.bz2, tar.lz, etc.) require random access to be decompressed, "
                        + "which is not supported by the Archive API. Please use ReaderFactory.Open() instead for forward-only extraction, "
                        + "or decompress the file first and then open the resulting tar file with ArchiveFactory.Open().";
                }
                return null;
            }

            stream.Seek(startPosition, SeekOrigin.Begin);

            // Check if it's an LZip file
            if (LZipStream.IsLZipFile(stream))
            {
                stream.Seek(startPosition, SeekOrigin.Begin);

                // Try to decompress and check if it contains a tar archive
                using var decompressed = new LZipStream(stream, CompressionMode.Decompress);
                if (IsTarStream(decompressed))
                {
                    return "This appears to be a tar.lz archive. Compressed tar formats (tar.bz2, tar.lz, etc.) require random access to be decompressed, "
                        + "which is not supported by the Archive API. Please use ReaderFactory.Open() instead for forward-only extraction, "
                        + "or decompress the file first and then open the resulting tar file with ArchiveFactory.Open().";
                }
                return null;
            }

            // Check file extension as a fallback for other compressed tar formats
            if (fileName != null)
            {
                var lowerFileName = fileName.ToLowerInvariant();
                if (
                    lowerFileName.EndsWith(".tar.bz2")
                    || lowerFileName.EndsWith(".tbz")
                    || lowerFileName.EndsWith(".tbz2")
                    || lowerFileName.EndsWith(".tb2")
                    || lowerFileName.EndsWith(".tz2")
                    || lowerFileName.EndsWith(".tar.lz")
                    || lowerFileName.EndsWith(".tar.xz")
                    || lowerFileName.EndsWith(".txz")
                    || lowerFileName.EndsWith(".tar.zst")
                    || lowerFileName.EndsWith(".tar.zstd")
                    || lowerFileName.EndsWith(".tzst")
                    || lowerFileName.EndsWith(".tzstd")
                    || lowerFileName.EndsWith(".tar.z")
                    || lowerFileName.EndsWith(".tz")
                    || lowerFileName.EndsWith(".taz")
                )
                {
                    return $"The file '{fileName}' appears to be a compressed tar archive. Compressed tar formats require random access to be decompressed, "
                        + "which is not supported by the Archive API. Please use ReaderFactory.Open() instead for forward-only extraction, "
                        + "or decompress the file first and then open the resulting tar file with ArchiveFactory.Open().";
                }
            }

            return null;
        }
        catch
        {
            // If we can't determine, just return null and let the normal error handling proceed
            return null;
        }
        finally
        {
            try
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
            }
            catch
            {
                // Ignore seek failures
            }
        }
    }

    /// <summary>
    /// Checks if a stream contains a tar archive by trying to read a tar header.
    /// </summary>
    private static bool IsTarStream(Stream stream)
    {
        try
        {
            var tarHeader = new TarHeader(new ArchiveEncoding());
            return tarHeader.Read(new BinaryReader(stream));
        }
        catch
        {
            return false;
        }
    }
}
