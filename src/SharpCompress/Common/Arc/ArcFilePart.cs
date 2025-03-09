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
                return new ReadOnlySubStream(_stream, Header.CompressedSize);
            }
            return _stream.NotNull();
        }

        internal override Stream? GetRawStream() => _stream;
    }
}
