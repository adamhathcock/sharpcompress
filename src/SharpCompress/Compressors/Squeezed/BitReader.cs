using System.IO;

namespace SharpCompress.Compressors.Squeezed
{
    // Helper BitReader class for reading individual bits
    public class BitReader
    {
        private readonly Stream _stream;
        private int _bitBuffer;
        private int _bitCount;

        public BitReader(Stream stream)
        {
            _stream = stream;
            _bitBuffer = 0;
            _bitCount = 0;
        }

        public bool ReadBit()
        {
            if (_bitCount == 0)
            {
                int nextByte = _stream.ReadByte();
                if (nextByte == -1)
                    throw new EndOfStreamException();
                _bitBuffer = nextByte;
                _bitCount = 8;
            }

            bool bit = (_bitBuffer & 1) != 0;
            _bitBuffer >>= 1;
            _bitCount--;
            return bit;
        }
    }
}
