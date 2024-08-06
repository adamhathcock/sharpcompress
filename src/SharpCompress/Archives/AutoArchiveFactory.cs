using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

class AutoArchiveFactory : IArchiveFactory
{
    public string Name => nameof(AutoArchiveFactory);

    public ArchiveType? KnownArchiveType => null;

    public IEnumerable<string> GetSupportedExtensions() => throw new NotSupportedException();

    public bool IsArchive(Stream stream, string? password = null) =>
        throw new NotSupportedException();

    public FileInfo? GetFilePart(int index, FileInfo part1) => throw new NotSupportedException();

    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.Open(stream, readerOptions);

    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ArchiveFactory.Open(fileInfo, readerOptions);
}
