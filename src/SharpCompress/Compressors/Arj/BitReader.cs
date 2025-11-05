using System;
using System.IO;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public sealed class BitReader
    {
        private readonly Stream _stream;
        private int _bitBuffer;
        private int _bitsRemaining;
        private bool _disposed;

        public BitReader(Stream input)
        {
            _stream = input ?? throw new ArgumentNullException(nameof(input));
            if (!input.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(input));
        }

        public int ReadBits(int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BitReader));

            if (count <= 0 || count > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    "Bit count must be between 1 and 32."
                );

            int result = 0;
            for (int i = 0; i < count; i++)
            {
                if (_bitsRemaining == 0)
                {
                    int nextByte = _stream.ReadByte();
                    if (nextByte == -1)
                        throw new EndOfStreamException();

                    _bitBuffer = nextByte;
                    _bitsRemaining = 8;
                }

                // hoogste bit eerst
                result = (result << 1) | ((_bitBuffer >> 7) & 1);
                _bitBuffer <<= 1;
                _bitsRemaining--;
            }

            return result;
        }

        public void AlignToByte()
        {
            _bitsRemaining = 0;
            _bitBuffer = 0;
        }
    }
}
