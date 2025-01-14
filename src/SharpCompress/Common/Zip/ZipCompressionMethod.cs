namespace SharpCompress.Common.Zip;

internal enum ZipCompressionMethod
{
    None = 0,
    Shrink = 1,
    Reduce1 = 2,
    Reduce2 = 3,
    Reduce3 = 4,
    Reduce4 = 5,
    Explode = 6,
    Deflate = 8,
    Deflate64 = 9,
    BZip2 = 12,
    LZMA = 14,
    ZStd = 93,
    Xz = 95,
    PPMd = 98,
    WinzipAes = 0x63, //http://www.winzip.com/aes_info.htm
}
