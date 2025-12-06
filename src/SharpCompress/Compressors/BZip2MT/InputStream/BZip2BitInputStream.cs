// Bzip2 library for .net
// By Jaime Olivares
// Location: http://github.com/jaime-olivares/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

using System.IO;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.InputStream
{
    /// <summary>
    /// Implements a bit-wise input stream
    /// </summary>
    internal class BZip2BitInputStream : IBZip2BitInputStream
    {
        // The stream from which bits are read
        private Stream _inputStream;

        // A buffer of bits read from the input stream that have not yet been returned
        private uint _bitBuffer;

        // The number of bits currently buffered in bitBuffer
        private int _bitCount;

        /// <summary>Public constructor</summary>
        /// <param name="inputStream">The input stream to wrap</param>
        public BZip2BitInputStream(Stream inputStream) => this._inputStream = inputStream;

        public void Dispose()
        {
            // do nothing, the BZip2BitInputStream is not the owner of the _inputStream so don't dispose it
        }

        /// <summary>Reads a single bit from the wrapped input stream</summary>
        /// <return>true if the bit read was 1, otherwise false</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        public bool ReadBoolean()
        {
            if (this._bitCount > 0)
            {
                this._bitCount--;
            } else
            {
                int byteRead = this._inputStream.ReadByte();

                if (byteRead < 0)
                    throw new IOException("Insufficient data");

                this._bitBuffer = (this._bitBuffer << 8) | (uint)byteRead;
                this._bitCount += 7;
            }

            return ((this._bitBuffer & (1 << this._bitCount))) != 0;
        }

        /// <summary>Reads a zero-terminated unary number from the wrapped input stream</summary>
        /// <return>The unary number</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        public uint ReadUnary()
        {
            for (uint unaryCount = 0; ; unaryCount++)
            {
                if (this._bitCount > 0)
                {
                    this._bitCount--;
                } else
                {
                    var byteRead = this._inputStream.ReadByte();

                    if (byteRead < 0)
                        throw new IOException("Insufficient data");

                    this._bitBuffer = (this._bitBuffer << 8) | (uint)byteRead;
                    this._bitCount += 7;
                }

                if (((this._bitBuffer & (1 << this._bitCount))) == 0)
                    return unaryCount;
            }
        }

        /// <summary>Reads up to 32 bits from the wrapped input stream</summary>
        /// <param name="count">The number of bits to read (maximum 32)</param>
        /// <return>The bits requested, right-aligned within the integer</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        public uint ReadBits(int count)
        {
            if (this._bitCount < count)
            {
                while (this._bitCount < count)
                {
                    int byteRead = this._inputStream.ReadByte();

                    if (byteRead < 0)
                        throw new IOException("Insufficient data");

                    this._bitBuffer = (this._bitBuffer << 8) | (uint)byteRead;
                    this._bitCount += 8;
                }
            }

            this._bitCount -= count;

            return (uint)((this._bitBuffer >> this._bitCount) & ((1 << count) - 1));
        }

        /// <summary>Reads 32 bits of input as an integer</summary>
        /// <return>The integer read</return>
        /// <exception cref="IOException">if 32 bits are not available in the input stream</exception>
        public uint ReadInteger() => (this.ReadBits(16) << 16) | (this.ReadBits(16));
    }
}
