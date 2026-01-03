using System.IO;

namespace SharpCompress.IO;

/// <summary>
/// A byte source backed by a file on the file system.
/// </summary>
public sealed class FileByteSource : IByteSource
{
    private readonly FileInfo _fileInfo;

    /// <summary>
    /// Creates a new file-based byte source.
    /// </summary>
    /// <param name="fileInfo">The file to read from.</param>
    /// <param name="index">The index of this source in a collection.</param>
    /// <param name="isPartOfContiguousSequence">Whether this is part of a split archive.</param>
    public FileByteSource(FileInfo fileInfo, int index = 0, bool isPartOfContiguousSequence = false)
    {
        _fileInfo = fileInfo;
        Index = index;
        IsPartOfContiguousSequence = isPartOfContiguousSequence;
    }

    /// <inheritdoc />
    public int Index { get; }

    /// <inheritdoc />
    public long? Length => _fileInfo.Exists ? _fileInfo.Length : null;

    /// <inheritdoc />
    public string? FileName => _fileInfo.FullName;

    /// <inheritdoc />
    public Stream OpenRead() => _fileInfo.OpenRead();

    /// <inheritdoc />
    public bool IsPartOfContiguousSequence { get; }
}
