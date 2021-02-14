using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Filters;
using SharpCompress.Compressors.LZMA.Utilites;
//using SharpCompress.Compressors.PPMd;

namespace SharpCompress.Compressors.LZMA
{
    internal static class DecoderRegistry
    {
        private const uint K_COPY = 0x0;
        private const uint K_DELTA = 3;
        private const uint K_LZMA2 = 0x21;
        private const uint K_LZMA = 0x030101;
        private const uint K_PPMD = 0x030401;
        private const uint K_BCJ = 0x03030103;
        private const uint K_BCJ2 = 0x0303011B;
        private const uint K_DEFLATE = 0x040108;
        private const uint K_B_ZIP2 = 0x040202;

        internal static async ValueTask<Stream> CreateDecoderStream(CMethodId id, Stream[] inStreams, byte[] info, IPasswordProvider pass,
                                                                    long limit, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            switch (id._id)
            {
                case K_COPY:
                    if (info != null)
                    {
                        throw new NotSupportedException();
                    }
                    return inStreams.Single();
                case K_LZMA:
                case K_LZMA2:
                    return await LzmaStream.CreateAsync(info, inStreams.Single(), -1, limit, cancellationToken: cancellationToken);
                case CMethodId.K_AES_ID:
                    return new AesDecoderStream(inStreams.Single(), info, pass, limit);
                case K_BCJ:
                    return new BCJFilter(false, inStreams.Single());
                case K_BCJ2:
                    return new Bcj2DecoderStream(inStreams, info, limit);
                /* case K_B_ZIP2:
                     return await BZip2Stream.CreateAsync(inStreams.Single(), CompressionMode.Decompress, true, cancellationToken);
                 case K_PPMD:
                     return new PpmdStream(new PpmdProperties(info), inStreams.Single(), false);*/
                case K_DEFLATE:
                    return new DeflateStream(inStreams.Single(), CompressionMode.Decompress);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}