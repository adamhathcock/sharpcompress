using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

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
        IWriteableArchiveFactory<TarWriterOptions>
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Tar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Tar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        foreach (var testOption in TarWrapper.Wrappers)
        {
            foreach (var ext in testOption.KnownExtensions)
            {
                yield return ext;
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null)
    {
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = wrapper.CreateStream(sharpCompressStream);
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.Rewind();
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (
                await wrapper
                    .IsMatchAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                sharpCompressStream.Rewind();
                var decompressedStream = await wrapper
                    .CreateStreamAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false);
                if (
                    await TarArchive
                        .IsTarFileAsync(decompressedStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    sharpCompressStream.Rewind();
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        readerOptions ??= new ReaderOptions();

        // Try to detect compressed TAR formats
        // For async-only streams, skip detection and assume uncompressed
        bool canDoSyncDetection = true;
        try
        {
            // Test if we can do synchronous reads
            var testBuffer = new byte[1];
            var pos = stream.Position;
            stream.Read(testBuffer, 0, 0); // Try a zero-length read
            stream.Position = pos;
        }
        catch (NotSupportedException)
        {
            // Stream doesn't support synchronous reads
            canDoSyncDetection = false;
        }

        if (!canDoSyncDetection)
        {
            // For async-only streams, we can't do format detection
            // Assume it's an uncompressed TAR
            return TarArchive.OpenArchive(stream, readerOptions);
        }

        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();

        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = wrapper.CreateStream(sharpCompressStream);
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.StopRecording();

                    // For compressed TAR files, we need to decompress to a seekable stream
                    // since Archive API requires seekable streams
                    if (wrapper.CompressionType != CompressionType.None)
                    {
                        // Rewind and create a fresh decompression stream
                        sharpCompressStream.Rewind();
                        decompressedStream = wrapper.CreateStream(sharpCompressStream);

                        // Decompress to a MemoryStream to make it seekable
                        var memoryStream = new MemoryStream();
                        decompressedStream.CopyTo(memoryStream);
                        memoryStream.Position = 0;

                        // If we shouldn't leave the stream open, close the original
                        if (!readerOptions.LeaveStreamOpen)
                        {
                            stream.Dispose();
                        }

                        // Open the decompressed TAR with LeaveStreamOpen = false
                        // so the MemoryStream gets cleaned up with the archive
                        return TarArchive.OpenArchive(
                            memoryStream,
                            readerOptions with
                            {
                                LeaveStreamOpen = false,
                            }
                        );
                    }

                    // For uncompressed TAR, use the original stream directly
                    sharpCompressStream.Rewind();
                    return TarArchive.OpenArchive(stream, readerOptions);
                }
            }
        }

        // Fallback: try opening as uncompressed TAR
        sharpCompressStream.StopRecording();
        return TarArchive.OpenArchive(stream, readerOptions);
    }

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        readerOptions ??= new ReaderOptions();

        // Open the file and check if it's compressed
        using var testStream = fileInfo.OpenRead();
        var sharpCompressStream = new SharpCompressStream(testStream);
        sharpCompressStream.StartRecording();

        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = wrapper.CreateStream(sharpCompressStream);
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.StopRecording();

                    // For compressed TAR files, decompress to memory
                    if (wrapper.CompressionType != CompressionType.None)
                    {
                        // Reopen file and decompress
                        using var fileStream = fileInfo.OpenRead();
                        var compressedStream = new SharpCompressStream(fileStream);
                        compressedStream.StartRecording();
                        compressedStream.Rewind();
                        var decompStream = wrapper.CreateStream(compressedStream);

                        var memoryStream = new MemoryStream();
                        decompStream.CopyTo(memoryStream);
                        memoryStream.Position = 0;

                        // Open with LeaveStreamOpen = false so MemoryStream gets cleaned up
                        return TarArchive.OpenArchive(
                            memoryStream,
                            readerOptions with
                            {
                                LeaveStreamOpen = false,
                            }
                        );
                    }

                    // Uncompressed, can use TarArchive's FileInfo overload directly
                    break;
                }
            }
        }

        // Open as regular TAR file
        return TarArchive.OpenArchive(fileInfo, readerOptions);
    }

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)OpenArchive(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => TarArchive.OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => TarArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options)
    {
        options ??= new ReaderOptions();
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = wrapper.CreateStream(sharpCompressStream);
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.StopRecording();
                    return new TarReader(sharpCompressStream, options, wrapper.CompressionType);
                }
            }
        }
        throw new InvalidFormatException("Not a tar file.");
    }

    /// <inheritdoc/>
    public async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ReaderOptions();
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (
                await wrapper
                    .IsMatchAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                sharpCompressStream.Rewind();
                var decompressedStream = await wrapper
                    .CreateStreamAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false);
                if (
                    await TarArchive
                        .IsTarFileAsync(decompressedStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    sharpCompressStream.Rewind();
                    sharpCompressStream.StopRecording();
                    return new TarReader(sharpCompressStream, options, wrapper.CompressionType);
                }
            }
        }
        return (IAsyncReader)TarReader.OpenReader(stream, options);
    }

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter OpenWriter(Stream stream, IWriterOptions writerOptions)
    {
        TarWriterOptions tarOptions = writerOptions switch
        {
            TarWriterOptions two => two,
            WriterOptions wo => new TarWriterOptions(wo),
            _ => throw new ArgumentException(
                $"Expected WriterOptions or TarWriterOptions, got {writerOptions.GetType().Name}",
                nameof(writerOptions)
            ),
        };
        return new TarWriter(stream, tarOptions);
    }

    /// <inheritdoc/>
    public IAsyncWriter OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive<TarWriterOptions> CreateArchive() => TarArchive.CreateArchive();

    #endregion
}
