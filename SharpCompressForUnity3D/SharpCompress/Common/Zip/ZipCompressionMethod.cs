namespace SharpCompress.Common.Zip
{
    using System;

    internal enum ZipCompressionMethod
    {
        BZip2 = 12,
        Deflate = 8,
        Deflate64 = 9,
        LZMA = 14,
        None = 0,
        PPMd = 0x62,
        WinzipAes = 0x63
    }
}

