using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Compressors.Arj;
using SharpCompress.IO;

namespace SharpCompress.Common.Arj
{
    public class ArjFilePart : FilePart
    {
        private readonly Stream _stream;
        internal ArjLocalHeader Header { get; set; }

        internal ArjFilePart(ArjLocalHeader localArjHeader, Stream seekableStream)
            : base(localArjHeader.ArchiveEncoding)
        {
            _stream = seekableStream;
            Header = localArjHeader;
        }

        internal override string? FilePartName => Header.Name;

        internal override Stream GetCompressedStream()
        {
            if (_stream != null)
            {
                Stream compressedStream;
                switch (Header.CompressionMethod)
                {
                    case CompressionMethod.Stored:
                        compressedStream = new ReadOnlySubStream(
                            _stream,
                            Header.DataStartPosition,
                            Header.CompressedSize
                        );
                        break;
                    case CompressionMethod.CompressedMost:
                    case CompressionMethod.Compressed:
                    case CompressionMethod.CompressedFaster:
                        if (Header.OriginalSize > 128 * 1024)
                        {
                            throw new NotSupportedException(
                                "CompressionMethod: "
                                    + Header.CompressionMethod
                                    + " with size > 128KB"
                            );
                        }
                        compressedStream = new LhaStream<Lh7DecoderCfg>(
                            _stream,
                            (int)Header.OriginalSize
                        );
                        break;
                    case CompressionMethod.CompressedFastest:
                        compressedStream = new LHDecoderStream(_stream, (int)Header.OriginalSize);
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

        internal override Stream GetRawStream() => _stream;
    }
}
