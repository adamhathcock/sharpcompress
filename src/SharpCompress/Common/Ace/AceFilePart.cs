using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents a file part within an ACE archive.
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
        if (_stream != null)
        {
            Stream compressedStream;
            switch (Header.CompressionMethod)
            {
                case CompressionType.None:
                    // Stored - no compression
                    compressedStream = new ReadOnlySubStream(
                        _stream,
                        Header.DataStartPosition,
                        Header.CompressedSize
                    );
                    break;
                default:
                    // ACE uses proprietary compression methods that are not publicly documented
                    // For now, we throw an exception for compressed entries
                    throw new NotSupportedException(
                        $"ACE compression method '{Header.CompressionMethod}' is not supported. Only stored (uncompressed) entries can be extracted."
                    );
            }
            return compressedStream;
        }
        return _stream.NotNull();
    }

    internal override Stream? GetRawStream() => _stream;
}
