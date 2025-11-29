using System.IO;

namespace SharpCompress.IO;

/// <summary>
/// A byte source backed by an existing stream.
/// </summary>
public sealed class StreamByteSource : IByteSource
{
    private readonly Stream _stream;

    /// <summary>
    /// Creates a new stream-based byte source.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="index">The index of this source in a collection.</param>
    /// <param name="isPartOfContiguousSequence">Whether this is part of a split archive.</param>
    public StreamByteSource(Stream stream, int index = 0, bool isPartOfContiguousSequence = false)
    {
        _stream = stream;
        Index = index;
        IsPartOfContiguousSequence = isPartOfContiguousSequence;

        if (stream is FileStream fileStream)
        {
            FileName = fileStream.Name;
        }
    }

    /// <inheritdoc />
    public int Index { get; }

    /// <inheritdoc />
    public long? Length
    {
        get
        {
            try
            {
                return _stream.CanSeek ? _stream.Length : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public string? FileName { get; }

    /// <inheritdoc />
    public Stream OpenRead()
    {
        if (_stream.CanSeek)
        {
            _stream.Position = 0;
        }
        return _stream;
    }

    /// <inheritdoc />
    public bool IsPartOfContiguousSequence { get; }
}
