using System;
using System.IO;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public class BitReader
    {
        private readonly Stream _input;
        private int _bitBuffer; // currently buffered bits
        private int _bitCount; // number of bits in buffer

        public BitReader(Stream input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _bitBuffer = 0;
            _bitCount = 0;
        }

        /// <summary>
        /// Reads a single bit from the stream. Returns 0 or 1.
        /// </summary>
        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                int nextByte = _input.ReadByte();
                if (nextByte < 0)
                {
                    throw new EndOfStreamException("No more data available in BitReader.");
                }

                _bitBuffer = nextByte;
                _bitCount = 8;
            }

            int bit = (_bitBuffer >> (_bitCount - 1)) & 1;
            _bitCount--;
            return bit;
        }

        /// <summary>
        /// Reads n bits (up to 32) from the stream.
        /// </summary>
        public int ReadBits(int count)
        {
            if (count < 0 || count > 32)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    "Count must be between 0 and 32."
                );
            }

            int result = 0;
            for (int i = 0; i < count; i++)
            {
                result = (result << 1) | ReadBit();
            }
            return result;
        }

        /// <summary>
        /// Resets any buffered bits.
        /// </summary>
        public void AlignToByte()
        {
            _bitCount = 0;
            _bitBuffer = 0;
        }
    }
}
