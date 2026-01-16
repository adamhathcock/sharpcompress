// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System.IO;
using SharpCompress.Compressors.BZip2MT.Interface;

namespace SharpCompress.Compressors.BZip2MT.OutputStream
{
    /// <summary>Implements a bit-wise output stream</summary>
    /// <remarks>
    /// Allows the writing of single bit booleans, unary numbers, bit
    /// strings of arbitrary length(up to 24 bits), and bit aligned 32-bit integers.A single byte at a
    /// time is written to the wrapped stream when sufficient bits have been accumulated
    /// </remarks>
    internal class BZip2BitOutputStream : IBZip2BitOutputStream
    {
        // The stream to which bits are written
        private readonly Stream _outputStream;

        // A buffer of bits waiting to be written to the output stream
        private uint _bitBuffer;

        // The number of bits currently buffered in bitBuffer
        private int _bitCount;

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="outputStream">The OutputStream to wrap</param>
        public BZip2BitOutputStream(Stream outputStream) => this._outputStream = outputStream;

        public void Dispose()
        {
            // do nothing, the BZip2BitOutputStream is not the owner of the _outputStream so don't dispose it
        }

        #region IBZip2BitOutputStream implementation

        public void WriteBoolean(bool value)
        {
            this._bitCount++;
            this._bitBuffer |= ((value ? 1u : 0u) << (32 - this._bitCount));

            if (this._bitCount == 8)
            {
                this._outputStream.WriteByte((byte)(this._bitBuffer >> 24));
                this._bitBuffer = 0;
                this._bitCount = 0;
            }
        }

        public void WriteUnary(int value)
        {
            while (value-- > 0)
            {
                this.WriteBoolean(true);
            }
            this.WriteBoolean(false);
        }

        public void WriteBits(int count, uint value)
        {
            this._bitBuffer |= ((value << (32 - count)) >> this._bitCount);
            this._bitCount += count;

            while (this._bitCount >= 8)
            {
                this._outputStream.WriteByte((byte)(this._bitBuffer >> 24));
                this._bitBuffer <<= 8;
                this._bitCount -= 8;
            }
        }

        public void WriteInteger(uint value)
        {
            this.WriteBits(16, (value >> 16) & 0xffff);
            this.WriteBits(16, value & 0xffff);
        }

        public void Flush()
        {
            if (this._bitCount > 0)
            {
                this.WriteBits(8 - this._bitCount, 0);
            }
        }

        #endregion
    }
}
