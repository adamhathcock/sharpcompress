namespace SharpCompress.Compressor.LZMA
{
    using System;

    internal class DataErrorException : Exception
    {
        public DataErrorException() : base("Data Error")
        {
        }
    }
}

