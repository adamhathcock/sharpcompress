using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.GZip;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.RLE90;
using SharpCompress.Compressors.Squeezed;
using SharpCompress.IO;

namespace SharpCompress.Common.Arc
{
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
                    case CompressionType.Packed:
                        compressedStream = new RunLength90Stream(
                            _stream,
                            (int)Header.CompressedSize
                        );
                        break;
                    case CompressionType.Squeezed:
                        compressedStream = new SqueezeStream(_stream, (int)Header.CompressedSize);
                        break;
                    case CompressionType.Crunched:
                        if (Header.OriginalSize > 128 * 1024)
                        {
                            throw new NotSupportedException(
                                "CompressionMethod: "
                                    + Header.CompressionMethod
                                    + " with size > 128KB"
                            );
                        }
                        compressedStream = new ArcLzwStream(
                            _stream,
                            (int)Header.CompressedSize,
                            true
                        );
                        break;
                    default:
                        throw new NotSupportedException(
                            "CompressionMethod: " + Header.CompressionMethod
                        );
                }
                return compressedStream;
            }
            return _stream.NotNull();
        }

        internal override Stream? GetRawStream() => _stream;
    }
}
