using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of ZIP archive.
/// </summary>
public class ZipFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Zip";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Zip;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "zip";
        yield return "zipx";
        yield return "cbz";
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        var startPosition = stream.CanSeek ? stream.Position : -1;

        // probe for single volume zip

        if (stream is not SharpCompressStream) // wrap to provide buffer bef
        {
            stream = new SharpCompressStream(stream, bufferSize: bufferSize);
        }

        if (ZipArchive.IsZipFile(stream, password, bufferSize))
        {
            return true;
        }

        // probe for a multipart zip

        if (!stream.CanSeek)
        {
            return false;
        }

        stream.Position = startPosition;

        //test the zip (last) file of a multipart zip
        if (ZipArchive.IsZipMulti(stream, password, bufferSize))
        {
            return true;
        }

        stream.Position = startPosition;

        return false;
    }

    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => new(IsArchive(stream, password, bufferSize));

    /// <inheritdoc/>
    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startPosition = stream.CanSeek ? stream.Position : -1;

        // probe for single volume zip

        if (stream is not SharpCompressStream) // wrap to provide buffer bef
        {
            stream = new SharpCompressStream(stream, bufferSize: bufferSize);
        }

        if (await ZipArchive.IsZipFileAsync(stream, password, bufferSize, cancellationToken))
        {
            return true;
        }

        // probe for a multipart zip

        if (!stream.CanSeek)
        {
            return false;
        }

        stream.Position = startPosition;

        //test the zip (last) file of a multipart zip
        if (await ZipArchive.IsZipMultiAsync(stream, password, bufferSize, cancellationToken))
        {
            return true;
        }

        stream.Position = startPosition;

        return false;
    }

    /// <inheritdoc/>
    public override FileInfo? GetFilePart(int index, FileInfo part1) =>
        ZipArchiveVolumeFactory.GetFilePart(index, part1);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(Stream stream, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfo, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfo, readerOptions);
    }

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfos, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfos, readerOptions);
    }

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        ZipReader.Open(stream, options);

    /// <inheritdoc/>
    public IAsyncReader OpenReaderAsync(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncReader)ZipReader.Open(stream, options);
    }

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions) =>
        new ZipWriter(stream, new ZipWriterOptions(writerOptions));

    /// <inheritdoc/>
    public IAsyncWriter OpenAsync(
        Stream stream,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)Open(stream, writerOptions);
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => ZipArchive.Create();

    #endregion
}
