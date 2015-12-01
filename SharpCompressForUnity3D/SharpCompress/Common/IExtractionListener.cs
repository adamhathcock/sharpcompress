namespace SharpCompress.Common
{
    using System;

    internal interface IExtractionListener
    {
        void FireCompressedBytesRead(long currentPartCompressedBytes, long compressedReadBytes);
        void FireFilePartExtractionBegin(string name, long size, long compressedSize);
    }
}

