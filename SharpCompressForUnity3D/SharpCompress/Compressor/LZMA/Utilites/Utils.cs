namespace SharpCompress.Compressor.LZMA.Utilites
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;

    //[Extension]
    internal static class Utils
    {
        [Conditional("DEBUG")]
        public static void Assert(bool expression)
        {
            if (!expression)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                throw new Exception("Assertion failed.");
            }
        }

        //[Extension]
        public static void ReadExact(Stream stream, byte[] buffer, int offset, int length)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if ((offset < 0) || (offset > buffer.Length))
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if ((length < 0) || (length > (buffer.Length - offset)))
            {
                throw new ArgumentOutOfRangeException("length");
            }
            while (length > 0)
            {
                int num = stream.Read(buffer, offset, length);
                if (num <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += num;
                length -= num;
            }
        }
    }
}

