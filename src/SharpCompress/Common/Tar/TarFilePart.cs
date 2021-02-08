using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Tar.Headers;

namespace SharpCompress.Common.Tar
{
    internal sealed class TarFilePart : FilePart
    {
        private readonly Stream _seekableStream;

        internal TarFilePart(TarHeader header, Stream seekableStream)
            : base(header.ArchiveEncoding)
        {
            _seekableStream = seekableStream;
            Header = header;
        }

        internal TarHeader Header { get; }

        internal override string FilePartName => Header.Name;

        internal override ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            if (_seekableStream != null)
            {
                _seekableStream.Position = Header.DataStartPosition!.Value;
                return new(new TarReadOnlySubStream(_seekableStream, Header.Size));
            }
            return new(Header.PackedStream);
        }

        internal override Stream? GetRawStream()
        {
            return null;
        }
    }
}