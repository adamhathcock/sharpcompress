using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        /// Asynchronously reads a single bit from the stream. Returns 0 or 1.
        /// </summary>
        public async ValueTask<int> ReadBitAsync(CancellationToken cancellationToken)
        {
            if (_bitCount == 0)
            {
                var buffer = new byte[1];
                int bytesRead = await _input
                    .ReadAsync(buffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead < 1)
                {
                    throw new EndOfStreamException("No more data available in BitReader.");
                }

                _bitBuffer = buffer[0];
                _bitCount = 8;
            }

            int bit = (_bitBuffer >> (_bitCount - 1)) & 1;
            _bitCount--;
            return bit;
        }

        /// <summary>
        /// Asynchronously reads n bits (up to 32) from the stream.
        /// </summary>
        public async ValueTask<int> ReadBitsAsync(int count, CancellationToken cancellationToken)
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
                result =
                    (result << 1) | await ReadBitAsync(cancellationToken).ConfigureAwait(false);
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
