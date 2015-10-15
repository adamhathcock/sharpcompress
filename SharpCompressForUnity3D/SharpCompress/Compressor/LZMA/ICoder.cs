namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.IO;

    internal interface ICoder
    {
        void Code(Stream inStream, Stream outStream, long inSize, long outSize, ICodeProgress progress);
    }
}

