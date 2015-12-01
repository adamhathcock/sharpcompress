namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Common.SevenZip;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.BZip2;
    using SharpCompress.Compressor.Filters;
    using SharpCompress.Compressor.LZMA.Utilites;
    using SharpCompress.Compressor.PPMd;
    using System;
    using System.IO;
    using System.Linq;

    internal static class DecoderRegistry
    {
        private const uint k_BCJ = 0x3030103;
        private const uint k_BCJ2 = 0x303011b;
        private const uint k_BZip2 = 0x40202;
        private const uint k_Copy = 0;
        private const uint k_Deflate = 0x40108;
        private const uint k_Delta = 3;
        private const uint k_LZMA = 0x30101;
        private const uint k_LZMA2 = 0x21;
        private const uint k_PPMD = 0x30401;

        internal static Stream CreateDecoderStream(CMethodId id, Stream[] inStreams, byte[] info, IPasswordProvider pass, long limit)
        {
            switch (id.Id)
            {
                case 0L:
                    if (info != null)
                    {
                        throw new NotSupportedException();
                    }
                    return Enumerable.Single<Stream>(inStreams);

                case 0x21L:
                case 0x30101L:
                    return new LzmaStream(info, Enumerable.Single<Stream>(inStreams), -1L, limit);

                case 0x3030103L:
                    return new BCJFilter(false, Enumerable.Single<Stream>(inStreams));

                case 0x303011bL:
                    return new Bcj2DecoderStream(inStreams, info, limit);

                case 0x30401L:
                    return new PpmdStream(new PpmdProperties(info), Enumerable.Single<Stream>(inStreams), false);

                case 0x40202L:
                    return new BZip2Stream(Enumerable.Single<Stream>(inStreams), CompressionMode.Decompress, true, false);
            }
            throw new NotSupportedException();
        }
    }
}

