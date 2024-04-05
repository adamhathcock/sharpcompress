using System;
using System.IO;
using System.Linq;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Filters;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.Compressors.PPMd;
using ZstdSharp;

namespace SharpCompress.Compressors.LZMA;

internal static class DecoderRegistry
{
    private const uint K_COPY = 0x0;
    private const uint K_DELTA = 0x3;
    private const uint K_LZMA2 = 0x21;
    private const uint K_LZMA = 0x030101;
    private const uint K_PPMD = 0x030401;
    private const uint K_BCJ = 0x03030103;
    private const uint K_BCJ2 = 0x0303011B;
    private const uint K_PPC = 0x03030205;
    private const uint K_IA64 = 0x03030401;
    private const uint K_ARM = 0x03030501;
    private const uint K_ARMT = 0x03030701;
    private const uint K_SPARC = 0x03030805;
    private const uint K_ARM64 = 0x0A;
    private const uint K_RISCV = 0x0B;
    private const uint K_DEFLATE = 0x040108;
    private const uint K_B_ZIP2 = 0x040202;
    private const uint K_ZSTD = 0x4F71101;

    internal static Stream CreateDecoderStream(
        CMethodId id,
        Stream[] inStreams,
        byte[] info,
        IPasswordProvider pass,
        long limit
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
                return new DeltaFilter(false, inStreams.Single(), info);
            case K_LZMA:
            case K_LZMA2:
                return new LzmaStream(info, inStreams.Single(), -1, limit);
            case CMethodId.K_AES_ID:
                return new AesDecoderStream(inStreams.Single(), info, pass, limit);
            case K_BCJ:
                return new BCJFilter(false, inStreams.Single());
            case K_BCJ2:
                return new Bcj2DecoderStream(inStreams, info, limit);
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
                return new BZip2Stream(inStreams.Single(), CompressionMode.Decompress, true);
            case K_PPMD:
                return new PpmdStream(new PpmdProperties(info), inStreams.Single(), false);
            case K_DEFLATE:
                return new DeflateStream(inStreams.Single(), CompressionMode.Decompress);
            case K_ZSTD:
                return new DecompressionStream(inStreams.Single());
            default:
                throw new NotSupportedException();
        }
    }
}
