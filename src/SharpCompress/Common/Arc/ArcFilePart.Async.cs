using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.ArcLzw;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.RLE90;
using SharpCompress.Compressors.Squeezed;
using SharpCompress.IO;

namespace SharpCompress.Common.Arc;

public partial class ArcFilePart
{
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
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
                    compressedStream = new RunLength90Stream(_stream, (int)Header.CompressedSize);
                    break;
                case CompressionType.Squeezed:
                    compressedStream = await SqueezeStream
                        .CreateAsync(_stream, (int)Header.CompressedSize, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CompressionType.Crunched:
                    if (Header.OriginalSize > 128 * 1024)
                    {
                        throw new NotSupportedException(
                            "CompressionMethod: " + Header.CompressionMethod + " with size > 128KB"
                        );
                    }
                    compressedStream = new ArcLzwStream(_stream, (int)Header.CompressedSize, true);
                    break;
                default:
                    throw new NotSupportedException(
                        "CompressionMethod: " + Header.CompressionMethod
                    );
            }
            return compressedStream;
        }
        return _stream;
    }
}
