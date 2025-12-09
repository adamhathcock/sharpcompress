using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using GZipArchive = SharpCompress.Archives.GZip.GZipArchive;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of TAR archive.
/// </summary>
public class TarFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Tar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Tar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        foreach (var testOption in compressionOptions)
        {
            foreach (var ext in testOption.KnownExtensions)
            {
                yield return ext;
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        if (!stream.CanSeek)
        {
            return TarArchive.IsTarFile(stream); // For non-seekable streams, just check if it's a tar file
        }

        var startPosition = stream.Position;

        // First check if it's a regular tar file
        if (TarArchive.IsTarFile(stream))
        {
            stream.Seek(startPosition, SeekOrigin.Begin); // Seek back for consistency
            return true;
        }

        // Seek back after the tar file check
        stream.Seek(startPosition, SeekOrigin.Begin);

        if (compressionOptions == null)
        {
            return false;
        }

        try
        {
            // Try each compression option to see if it contains a tar file
            foreach (var testOption in compressionOptions)
            {
                if (testOption.Type == CompressionType.None)
                {
                    continue; // Skip uncompressed
                }

                stream.Seek(startPosition, SeekOrigin.Begin);

                try
                {
                    if (testOption.CanHandle(stream))
                    {
                        stream.Seek(startPosition, SeekOrigin.Begin);

                        // Try to decompress and check if it contains a tar archive
                        // For compression formats that don't support leaveOpen, we need to save/restore position
                        var positionBeforeDecompress = stream.Position;
                        Stream? decompressedStream = null;
                        bool streamWasClosed = false;
                        
                        try
                        {
                            decompressedStream = testOption.Type switch
                            {
                                CompressionType.BZip2 => new BZip2Stream(stream, CompressionMode.Decompress, true),
                                _ => testOption.CreateStream(stream) // For other types, may close the stream
                            };

                            if (TarArchive.IsTarFile(decompressedStream))
                            {
                                return true;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            streamWasClosed = true;
                            throw; // Stream was closed, can't continue
                        }
                        finally
                        {
                            decompressedStream?.Dispose();
                            
                            if (!streamWasClosed && stream.CanSeek)
                            {
                                try
                                {
                                    stream.Seek(positionBeforeDecompress, SeekOrigin.Begin);
                                }
                                catch
                                {
                                    // If seek fails, the stream might have been closed
                                }
                            }
                        }

                        // Seek back to start after decompression attempt
                        stream.Seek(startPosition, SeekOrigin.Begin);
                    }
                }
                catch
                {
                    // If decompression fails, it's not this format - continue to next option
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

            return false;
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

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= new ReaderOptions();

        // Try to detect and handle compressed tar formats
        if (stream.CanSeek)
        {
            var startPosition = stream.Position;

            // Try each compression option to see if we can decompress it
            foreach (var testOption in compressionOptions)
            {
                if (testOption.Type == CompressionType.None)
                {
                    continue; // Skip uncompressed
                }

                stream.Seek(startPosition, SeekOrigin.Begin);

                if (testOption.CanHandle(stream))
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);

                    // Decompress the entire stream into a seekable MemoryStream
                    using var decompressedStream = testOption.CreateStream(stream);
                    var memoryStream = new MemoryStream();
                    decompressedStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    // Verify it's actually a tar file
                    if (TarArchive.IsTarFile(memoryStream))
                    {
                        memoryStream.Position = 0;
                        // Return a TarArchive from the decompressed memory stream
                        // The TarArchive will own the MemoryStream and dispose it when disposed
                        var options = new ReaderOptions
                        {
                            LeaveStreamOpen = false, // Ensure the MemoryStream is disposed with the archive
                            ArchiveEncoding = readerOptions?.ArchiveEncoding ?? new ArchiveEncoding()
                        };
                        return TarArchive.Open(memoryStream, options);
                    }

                    memoryStream.Dispose();
                }
            }

            stream.Seek(startPosition, SeekOrigin.Begin);
        }

        // Fall back to normal tar archive opening
        return TarArchive.Open(stream, readerOptions);
    }

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= new ReaderOptions();

        // Try to detect and handle compressed tar formats by file extension and content
        using var fileStream = fileInfo.OpenRead();

        // Try each compression option
        foreach (var testOption in compressionOptions)
        {
            if (testOption.Type == CompressionType.None)
            {
                continue; // Skip uncompressed
            }

            // Check if file extension matches
            var fileName = fileInfo.Name.ToLowerInvariant();
            if (testOption.KnownExtensions.Any(ext => fileName.EndsWith(ext)))
            {
                fileStream.Position = 0;

                // Verify it's the right compression format
                if (testOption.CanHandle(fileStream))
                {
                    fileStream.Position = 0;

                    // Decompress the entire file into a seekable MemoryStream
                    using var decompressedStream = testOption.CreateStream(fileStream);
                    var memoryStream = new MemoryStream();
                    decompressedStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    // Verify it's actually a tar file
                    if (TarArchive.IsTarFile(memoryStream))
                    {
                        memoryStream.Position = 0;
                        // Return a TarArchive from the decompressed memory stream
                        // The TarArchive will own the MemoryStream and dispose it when disposed
                        var options = new ReaderOptions
                        {
                            LeaveStreamOpen = false, // Ensure the MemoryStream is disposed with the archive
                            ArchiveEncoding = readerOptions?.ArchiveEncoding ?? new ArchiveEncoding()
                        };
                        return TarArchive.Open(memoryStream, options);
                    }

                    memoryStream.Dispose();
                }
            }
        }

        // fileStream will be closed by the using statement

        // Fall back to normal tar archive opening
        return TarArchive.Open(fileInfo, readerOptions);
    }

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory


    protected class TestOption
    {
        public readonly CompressionType Type;
        public readonly Func<Stream, bool> CanHandle;
        public readonly bool WrapInSharpCompressStream;

        public readonly Func<Stream, Stream> CreateStream;

        public readonly IEnumerable<string> KnownExtensions;

        public TestOption(
            CompressionType Type,
            Func<Stream, bool> CanHandle,
            Func<Stream, Stream> CreateStream,
            IEnumerable<string> KnownExtensions,
            bool WrapInSharpCompressStream = true
        )
        {
            this.Type = Type;
            this.CanHandle = CanHandle;
            this.WrapInSharpCompressStream = WrapInSharpCompressStream;
            this.CreateStream = CreateStream;
            this.KnownExtensions = KnownExtensions;
        }
    }

    // https://en.wikipedia.org/wiki/Tar_(computing)#Suffixes_for_compressed_files
    protected TestOption[] compressionOptions =
    [
        new(CompressionType.None, (stream) => true, (stream) => stream, ["tar"], false), // We always do a test for IsTarFile later
        new(
            CompressionType.BZip2,
            BZip2Stream.IsBZip2,
            (stream) => new BZip2Stream(stream, CompressionMode.Decompress, false),
            ["tar.bz2", "tb2", "tbz", "tbz2", "tz2"]
        ),
        new(
            CompressionType.GZip,
            GZipArchive.IsGZipFile,
            (stream) => new GZipStream(stream, CompressionMode.Decompress),
            ["tar.gz", "taz", "tgz"]
        ),
        new(
            CompressionType.ZStandard,
            ZStandardStream.IsZStandard,
            (stream) => new ZStandardStream(stream),
            ["tar.zst", "tar.zstd", "tzst", "tzstd"]
        ),
        new(
            CompressionType.LZip,
            LZipStream.IsLZipFile,
            (stream) => new LZipStream(stream, CompressionMode.Decompress),
            ["tar.lz"]
        ),
        new(
            CompressionType.Xz,
            XZStream.IsXZStream,
            (stream) => new XZStream(stream),
            ["tar.xz", "txz"],
            false
        ),
        new(
            CompressionType.Lzw,
            LzwStream.IsLzwStream,
            (stream) => new LzwStream(stream),
            ["tar.Z", "tZ", "taZ"],
            false
        ),
    ];

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;
        long pos = ((IStreamStack)rewindableStream).GetPosition();
        TestOption? testedOption = null;
        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedOption = compressionOptions.FirstOrDefault(a =>
                a.KnownExtensions.Contains(
                    options.ExtensionHint,
                    StringComparer.CurrentCultureIgnoreCase
                )
            );
            if (testedOption != null)
            {
                reader = TryOption(rewindableStream, options, pos, testedOption);
                if (reader != null)
                {
                    return true;
                }
            }
        }

        foreach (var testOption in compressionOptions)
        {
            if (testedOption == testOption)
            {
                continue; // Already tested above
            }
            ((IStreamStack)rewindableStream).StackSeek(pos);
            reader = TryOption(rewindableStream, options, pos, testOption);
            if (reader != null)
            {
                return true;
            }
        }

        return false;
    }

    private static IReader? TryOption(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        long pos,
        TestOption testOption
    )
    {
        if (testOption.CanHandle(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var inStream = rewindableStream;
            if (testOption.WrapInSharpCompressStream)
            {
                inStream = SharpCompressStream.Create(rewindableStream, leaveOpen: true);
            }
            var testStream = testOption.CreateStream(rewindableStream);

            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, testOption.Type);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        TarReader.Open(stream, options);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions) =>
        new TarWriter(stream, new TarWriterOptions(writerOptions));

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => TarArchive.Create();

    #endregion
}
