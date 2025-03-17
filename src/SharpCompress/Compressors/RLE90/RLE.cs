using System.Collections.Generic;
using System.Linq;

namespace SharpCompress.Compressors.RLE90
{
    public static class RLE
    {
        private const byte DLE = 0x90;

        /// <summary>
        /// Unpacks an RLE compressed buffer.
        /// Format: <char> DLE <count>, where count == 0 -> DLE
        /// </summary>
        /// <param name="compressedBuffer">The compressed buffer to unpack.</param>
        /// <returns>A list of unpacked bytes.</returns>
        public static List<byte> UnpackRLE(byte[] compressedBuffer)
        {
            var result = new List<byte>(compressedBuffer.Length * 2); // Optimized initial capacity
            var countMode = false;
            byte last = 0;

            foreach (var c in compressedBuffer)
            {
                if (!countMode)
                {
                    if (c == DLE)
                    {
                        countMode = true;
                    }
                    else
                    {
                        result.Add(c);
                        last = c;
                    }
                }
                else
                {
                    countMode = false;
                    if (c == 0)
                    {
                        result.Add(DLE);
                    }
                    else
                    {
                        result.AddRange(Enumerable.Repeat(last, c - 1));
                    }
                }
            }
            return result;
        }
    }
}
