namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.IO;

    internal interface IWriteCoderProperties
    {
        void WriteCoderProperties(Stream outStream);
    }
}

