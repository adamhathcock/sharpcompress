using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents a file part within an ACE archive.
/// Supports both ACE 1.0 and ACE 2.0 formats.
/// </summary>
public class AceFilePart : FilePart
{
    private readonly Stream? _stream;

    internal AceFilePart(AceEntryHeader header, Stream? stream)
        : base(header.ArchiveEncoding)
    {
        _stream = stream;
        Header = header;
    }

    internal AceEntryHeader Header { get; }

    internal override string? FilePartName => Header.Name;

    internal override Stream GetCompressedStream()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Stream is not available.");
        }

        switch (Header.CompressionMethod)
        {
            case CompressionType.None:
                // Stored - no compression
                return new ReadOnlySubStream(
                    _stream,
                    Header.DataStartPosition,
                    Header.CompressedSize
                );
            case CompressionType.Ace:
            case CompressionType.Ace2:
                // ACE 1.0 and 2.0 use proprietary compression methods
                // The algorithms are not publicly documented
                throw new NotSupportedException(
                    $"ACE compression method '{Header.CompressionMethod}' is not supported. "
                        + "Only stored (uncompressed) entries can be extracted. "
                        + "ACE uses proprietary compression algorithms that are not publicly documented."
                );
            default:
                throw new NotSupportedException(
                    $"ACE compression method '{Header.CompressionMethod}' is not supported. Only stored (uncompressed) entries can be extracted."
                );
        }
    }

    internal override Stream? GetRawStream() => _stream;
}
