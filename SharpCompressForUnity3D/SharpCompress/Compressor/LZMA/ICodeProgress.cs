namespace SharpCompress.Compressor.LZMA
{
    using System;

    internal interface ICodeProgress
    {
        void SetProgress(long inSize, long outSize);
    }
}

