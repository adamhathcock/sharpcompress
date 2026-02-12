using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Options;
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
        IWriteableArchiveFactory<ZipWriterOptions>
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
    public override bool IsArchive(Stream stream, string? password = null)
    {
        var startPosition = stream.CanSeek ? stream.Position : -1;
        if (ZipArchive.IsZipFile(stream, password))
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
        if (ZipArchive.IsZipMulti(stream, password))
        {
            return true;
        }

        stream.Position = startPosition;

        return false;
    }

    /// <inheritdoc/>
    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startPosition = stream.CanSeek ? stream.Position : -1;

        // probe for single volume zip
        if (
            await ZipArchive
                .IsZipFileAsync(stream, password, cancellationToken)
                .ConfigureAwait(false)
        )
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
        if (
            await ZipArchive
                .IsZipMultiAsync(stream, password, cancellationToken)
                .ConfigureAwait(false)
        )
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
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        ZipArchive.OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(stream, readerOptions));
    }

    /// <inheritdoc/>
    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ZipArchive.OpenArchive(fileInfo, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(fileInfo, readerOptions));
    }

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => ZipArchive.OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await ZipArchive
            .OpenAsyncArchive(streams, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => ZipArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await ZipArchive
            .OpenAsyncArchive(fileInfos, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        ZipReader.OpenReader(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)ZipReader.OpenReader(stream, options));
    }

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter OpenWriter(Stream stream, IWriterOptions writerOptions)
    {
        ZipWriterOptions zipOptions = writerOptions switch
        {
            ZipWriterOptions zwo => zwo,
            WriterOptions wo => new ZipWriterOptions(wo),
            _ => throw new ArgumentException(
                $"Expected WriterOptions or ZipWriterOptions, got {writerOptions.GetType().Name}",
                nameof(writerOptions)
            ),
        };
        return new ZipWriter(stream, zipOptions);
    }

    /// <inheritdoc/>
    public ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var writer = OpenWriter(stream, writerOptions);
        return new((IAsyncWriter)writer);
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive<ZipWriterOptions> CreateArchive() => ZipArchive.CreateArchive();

    #endregion
}
