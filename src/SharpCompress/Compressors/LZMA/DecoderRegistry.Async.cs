using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Filters;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Compressors.LZMA;

internal static partial class DecoderRegistry
{
    internal static async ValueTask<Stream> CreateDecoderStreamAsync(
        CMethodId id,
        Stream[] inStreams,
        byte[]? info,
        IPasswordProvider pass,
        long limit,
        CancellationToken cancellationToken
    )
    {
        switch (id._id)
        {
            case K_COPY:
                if (info != null)
                {
                    throw new NotSupportedException();
                }
                return inStreams.Single();
            case K_DELTA:
                return new DeltaFilter(false, inStreams.Single(), info.NotNull());
            case K_LZMA:
            case K_LZMA2:
                return await LzmaStream
                    .CreateAsync(
                        info.NotNull(),
                        inStreams.Single(),
                        -1,
                        limit,
                        null,
                        info.NotNull().Length < 5,
                        false
                    )
                    .ConfigureAwait(false);
            case CMethodId.K_AES_ID:
                return new AesDecoderStream(inStreams.Single(), info.NotNull(), pass, limit);
            case K_BCJ:
                return new BCJFilter(false, inStreams.Single());
            case K_BCJ2:
                return new Bcj2DecoderStream(inStreams);
            case K_PPC:
                return new BCJFilterPPC(false, inStreams.Single());
            case K_IA64:
                return new BCJFilterIA64(false, inStreams.Single());
            case K_ARM:
                return new BCJFilterARM(false, inStreams.Single());
            case K_ARMT:
                return new BCJFilterARMT(false, inStreams.Single());
            case K_SPARC:
                return new BCJFilterSPARC(false, inStreams.Single());
            case K_ARM64:
                return new BCJFilterARM64(false, inStreams.Single());
            case K_RISCV:
                return new BCJFilterRISCV(false, inStreams.Single());
            case K_B_ZIP2:
                return await BZip2Stream
                    .CreateAsync(
                        inStreams.Single(),
                        CompressionMode.Decompress,
                        true,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            case K_PPMD:
                return await PpmdStream
                    .CreateAsync(
                        new PpmdProperties(info.NotNull()),
                        inStreams.Single(),
                        false,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            case K_DEFLATE:
                return new DeflateStream(inStreams.Single(), CompressionMode.Decompress);
            case K_ZSTD:
                return new DecompressionStream(inStreams.Single());
            default:
                throw new NotSupportedException();
        }
    }
}
