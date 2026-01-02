using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

class AutoArchiveFactory : IArchiveFactory
{
    public string Name => nameof(AutoArchiveFactory);

    public ArchiveType? KnownArchiveType => null;

    public IEnumerable<string> GetSupportedExtensions() => throw new NotSupportedException();

    public bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => throw new NotSupportedException();

    public Task<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public FileInfo? GetFilePart(int index, FileInfo part1) => throw new NotSupportedException();

    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.Open(stream, readerOptions);

    public Task<IArchive> OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => ArchiveFactory.OpenAsync(stream, readerOptions, cancellationToken);

    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.Open(fileInfo, readerOptions);

    public Task<IArchive> OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => ArchiveFactory.OpenAsync(fileInfo, readerOptions, cancellationToken);
}
