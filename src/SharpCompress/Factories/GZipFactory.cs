using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of GZip archive.
/// </summary>
public class GZipFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "GZip";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.GZip;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "gz";
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => GZipArchive.IsGZipFile(stream);

    /// <inheritdoc/>
    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    ) => GZipArchive.IsGZipFileAsync(stream, cancellationToken);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => GZipArchive.OpenAsync(stream, readerOptions, cancellationToken);

    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => new(IsArchive(stream, password, bufferSize));

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(fileInfo, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => GZipArchive.OpenAsync(fileInfo, readerOptions, cancellationToken);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => GZipArchive.OpenAsync(streams, readerOptions, cancellationToken);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(fileInfos, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => GZipArchive.OpenAsync(fileInfos, readerOptions, cancellationToken);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        long pos = ((IStreamStack)rewindableStream).GetPosition();

        if (GZipArchive.IsGZipFile(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                reader = new TarReader(rewindableStream, options, CompressionType.GZip);
                return true;
            }

            ((IStreamStack)rewindableStream).StackSeek(pos);
            reader = OpenReader(rewindableStream, options);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        GZipReader.Open(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenReaderAsync(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(GZipReader.Open(stream, options));
    }

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions)
    {
        if (writerOptions.CompressionType != CompressionType.GZip)
        {
            throw new InvalidFormatException("GZip archives only support GZip compression type.");
        }
        return new GZipWriter(stream, new GZipWriterOptions(writerOptions));
    }

    /// <inheritdoc/>
    public ValueTask<IWriter> OpenAsync(
        Stream stream,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Open(stream, writerOptions));
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => GZipArchive.Create();

    #endregion
}
