using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of RAR archive.
/// </summary>
public class RarFactory : Factory, IArchiveFactory, IMultiArchiveFactory, IReaderFactory
{
    #region IArchive

    /// <inheritdoc/>
    public override string Name => "Rar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Rar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "rar";
        yield return "cbr";
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => RarArchive.IsRarFile(stream);

    /// <inheritdoc/>
    public override FileInfo? GetFilePart(int index, FileInfo part1) =>
        RarArchiveVolumeFactory.GetFilePart(index, part1);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => RarArchive.OpenAsync(stream, readerOptions, cancellationToken);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(fileInfo, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => RarArchive.OpenAsync(fileInfo, readerOptions, cancellationToken);

    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => new(IsArchive(stream, password, bufferSize));

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => RarArchive.OpenAsync(streams, readerOptions, cancellationToken);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(fileInfos, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => RarArchive.OpenAsync(fileInfos, readerOptions, cancellationToken);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        RarReader.Open(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenReaderAsync(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(RarReader.Open(stream, options));
    }

    #endregion
}
