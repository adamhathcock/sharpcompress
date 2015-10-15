namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal static class ZlibConstants
    {
        public const int WindowBitsDefault = 15;
        public const int WindowBitsMax = 15;
        public const int WorkingBufferSizeDefault = 0x4000;
        public const int WorkingBufferSizeMin = 0x400;
        public const int Z_BUF_ERROR = -5;
        public const int Z_DATA_ERROR = -3;
        public const int Z_NEED_DICT = 2;
        public const int Z_OK = 0;
        public const int Z_STREAM_END = 1;
        public const int Z_STREAM_ERROR = -2;
    }
}

