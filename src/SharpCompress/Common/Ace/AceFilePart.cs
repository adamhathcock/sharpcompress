using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Ace
{
    public class AceFilePart : FilePart
    {
        private readonly Stream _stream;
        internal AceFileHeader Header { get; set; }

        internal AceFilePart(AceFileHeader localAceHeader, Stream seekableStream)
            : base(localAceHeader.ArchiveEncoding)
        {
            _stream = seekableStream;
            Header = localAceHeader;
        }

        internal override string? FilePartName => Header.Filename;

        internal override Stream GetCompressedStream()
        {
            if (_stream != null)
            {
                Stream compressedStream;
                switch (Header.CompressionType)
                {
                    case Headers.CompressionType.Stored:
                        compressedStream = new ReadOnlySubStream(
                            _stream,
                            Header.DataStartPosition,
                            Header.PackedSize
                        );
                        break;
                    default:
                        throw new NotSupportedException(
                            "CompressionMethod: " + Header.CompressionQuality
                        );
                }
                return compressedStream;
            }
            return _stream.NotNull();
        }

        internal override Stream? GetRawStream() => _stream;
    }
}
