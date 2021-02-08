using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal sealed class StreamingZipFilePart : ZipFilePart
    {
        private Stream? _decompressionStream;

        internal StreamingZipFilePart(ZipFileEntry header, Stream stream)
            : base(header, stream)
        {
        }

        protected override Stream CreateBaseStream()
        {
            return Header.PackedStream;
        }

        internal override async ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            if (!Header.HasData)
            {
                return Stream.Null;
            }
            _decompressionStream = await CreateDecompressionStream(GetCryptoStream(CreateBaseStream()), Header.CompressionMethod, cancellationToken);
            if (LeaveStreamOpen)
            {
                return new NonDisposingStream(_decompressionStream);
            }
            return _decompressionStream;
        }

        internal async ValueTask FixStreamedFileLocation(RewindableStream rewindableStream, CancellationToken cancellationToken)
        {
            if (Header.IsDirectory)
            {
                return;
            }
            if (Header.HasData && !Skipped)
            {
                _decompressionStream ??= await GetCompressedStreamAsync(cancellationToken);

                await _decompressionStream.SkipAsync(cancellationToken);

                if (_decompressionStream is DeflateStream deflateStream)
                {
                    rewindableStream.Rewind(deflateStream.InputBuffer);
                }
                Skipped = true;
            }
            _decompressionStream = null;
        }
    }
}