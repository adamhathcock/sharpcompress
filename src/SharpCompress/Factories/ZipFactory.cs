using System;
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

    public override async Task<bool> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken,
        string? password = null
    )
    {
        var startPosition = stream.CanSeek ? stream.Position : -1;

        if (stream is not SharpCompressStream)
        {
            stream = new SharpCompressStream(
                stream,
                bufferSize: ReaderOptions.DefaultBufferSize
            );
        }

        if (await ZipArchive.IsZipFileAsync(stream, password, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!stream.CanSeek)
        {
            return false;
        }

        stream.Position = startPosition;

        if (await ZipArchive.IsZipMultiAsync(stream, password, cancellationToken).ConfigureAwait(false))
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

    public Task<IArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfo, readerOptions);

    public Task<IArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(streams, readerOptions);

    public Task<IArchive> OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfos, readerOptions);

    public Task<IArchive> OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        ZipReader.Open(stream, options);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions) =>
        new ZipWriter(stream, new ZipWriterOptions(writerOptions));

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => ZipArchive.Create();

    #endregion
}
