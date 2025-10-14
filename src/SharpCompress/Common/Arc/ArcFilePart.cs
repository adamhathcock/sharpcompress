using System;
using System.IO;
using SharpCompress.Compressors.ArcLzw;
using SharpCompress.Compressors.RLE90;
using SharpCompress.Compressors.Squeezed;
using SharpCompress.IO;

namespace SharpCompress.Common.Arc;

public class ArcFilePart : FilePart
{
  private readonly Stream? _stream;

  internal ArcFilePart(ArcEntryHeader localArcHeader, Stream? seekableStream)
    : base(localArcHeader.ArchiveEncoding)
  {
    _stream = seekableStream;
    Header = localArcHeader;
  }

  internal ArcEntryHeader Header { get; set; }

  internal override string? FilePartName => Header.Name;

  internal override Stream GetCompressedStream()
  {
    if (_stream != null)
    {
      Stream compressedStream;
      switch (Header.CompressionMethod)
      {
        case CompressionType.None:
          compressedStream = new ReadOnlySubStream(
            _stream,
            Header.DataStartPosition,
            Header.CompressedSize
          );
          break;
        case CompressionType.RLE90:
          compressedStream = new RunLength90Stream(_stream, (int)Header.CompressedSize);
          break;
        case CompressionType.Squeezed:
          compressedStream = new SqueezeStream(_stream, (int)Header.CompressedSize);
          break;
        case CompressionType.Crunched:
          compressedStream = new ArcLzwStream(_stream, (int)Header.CompressedSize);
          break;
        default:
          throw new NotSupportedException("CompressionMethod: " + Header.CompressionMethod);
      }
      return compressedStream;
    }
    return _stream.NotNull();
  }

  internal override Stream? GetRawStream() => _stream;
}
