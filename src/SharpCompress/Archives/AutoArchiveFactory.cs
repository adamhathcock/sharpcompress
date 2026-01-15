using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

internal class AutoArchiveFactory : IArchiveFactory
{
    public string Name => nameof(AutoArchiveFactory);

    public ArchiveType? KnownArchiveType => null;

    public IEnumerable<string> GetSupportedExtensions() => throw new NotSupportedException();

    public bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => throw new NotSupportedException();

    public ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public FileInfo? GetFilePart(int index, FileInfo part1) => throw new NotSupportedException();

    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.OpenArchive(stream, readerOptions);

    public IAsyncArchive OpenAsyncArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)OpenArchive(stream, readerOptions);

    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.OpenArchive(fileInfo, readerOptions);

    public IAsyncArchive OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)OpenArchive(fileInfo, readerOptions);
    }
}
