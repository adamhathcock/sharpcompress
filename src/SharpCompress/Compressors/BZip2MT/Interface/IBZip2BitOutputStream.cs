// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;

namespace SharpCompress.Compressors.BZip2MT.Interface
{
    /// <summary>
    /// Interface for a stream wrapper that implements bit-wise write operations
    /// </summary>
    internal interface IBZip2BitOutputStream : IDisposable
    {
        /// <summary>
        /// Writes a single bit to the wrapped output stream
        /// </summary>
        /// <param name="value">The bit to write</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteBoolean(bool value);

        /// <summary>
        /// Writes a zero-terminated unary number to the wrapped output stream
        /// </summary>
        /// <param name="value">The number to write (must be non-negative)</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteUnary(int value);

        /// <summary>
        /// Writes up to 24 bits to the wrapped output stream
        /// </summary>
        /// <param name="count">The number of bits to write (maximum 24)</param>
        /// <param name="value">The bits to write</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteBits(int count, uint value);

        /// <summary>
        /// Writes an integer as 32 bits of output
        /// </summary>
        /// <param name="value">The integer to write</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteInteger(uint value);

        /// <summary>
        /// Writes any remaining bits to the output stream, zero padding to a whole byte as required
        /// </summary>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void Flush();
    }
}
