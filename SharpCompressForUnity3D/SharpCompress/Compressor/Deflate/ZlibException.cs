namespace SharpCompress.Compressor.Deflate
{
    using System;

    public class ZlibException : Exception
    {
        public ZlibException()
        {
        }

        public ZlibException(string s) : base(s)
        {
        }
    }
}

