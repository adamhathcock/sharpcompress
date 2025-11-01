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
                    case CompressionMethod.CompressedFastest:
                        byte[] compressedData = new byte[Header.CompressedSize];
                        _stream.Position = Header.DataStartPosition;
                        _stream.Read(compressedData, 0, compressedData.Length);

                        byte[] decompressedData = LHDecoder.DecodeFastest(
                            compressedData,
                            (int)Header.OriginalSize // ARJ can only handle files up to 2GB, so casting to int should not be an issue.
                        );

                        compressedStream = new MemoryStream(decompressedData);
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
