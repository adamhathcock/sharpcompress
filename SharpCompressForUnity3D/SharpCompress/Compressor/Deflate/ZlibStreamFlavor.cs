namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal enum ZlibStreamFlavor
    {
        DEFLATE = 0x79f,
        GZIP = 0x7a0,
        ZLIB = 0x79e
    }
}

