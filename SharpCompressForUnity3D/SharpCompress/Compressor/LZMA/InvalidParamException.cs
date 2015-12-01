namespace SharpCompress.Compressor.LZMA
{
    using System;

    internal class InvalidParamException : Exception
    {
        public InvalidParamException() : base("Invalid Parameter")
        {
        }
    }
}

