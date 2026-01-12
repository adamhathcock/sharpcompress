using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of 7Zip archive.
/// </summary>
public class SevenZipFactory : Factory, IArchiveFactory, IMultiArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "7Zip";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.SevenZip;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "7z";
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => SevenZipArchive.IsSevenZipFile(stream);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        SevenZipArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(Stream stream, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        SevenZipArchive.Open(fileInfo, readerOptions);

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

    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => new(IsArchive(stream, password, bufferSize));

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        SevenZipArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        SevenZipArchive.Open(fileInfos, readerOptions);

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

    #region reader

    internal override bool TryOpenReader(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;
        return false;
    }

    #endregion
}
