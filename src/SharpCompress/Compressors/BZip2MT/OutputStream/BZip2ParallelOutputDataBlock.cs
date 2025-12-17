// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;
using System.IO;
using SharpCompress.Compressors.BZip2MT.Algorithm;
using SharpCompress.Compressors.BZip2MT.Interface;

namespace SharpCompress.Compressors.BZip2MT.OutputStream
{
    /// <summary>A collection of bit output data</summary>
    /// <remarks>
    /// Allows the writing of single bit booleans, unary numbers, bit
    /// strings of arbitrary length(up to 24 bits), and bit aligned 32-bit integers.A single byte at a
    /// time is written to a list of structures that serves as a buffer for use in parallelized
    /// execution of block compression
    /// </remarks>
    internal class BZip2ParallelOutputDataBlock : IBZip2BitOutputStream
    {
        private BZip2BitOutputStream _internalBitStream;
        private MemoryStream _buffer;
        private long _bitCount = 0;

        /// <summary>
        /// Compressed block CRC to be stored here when block is finished
        /// </summary>
        public uint BlockCrc => this._blockCrc;
        private uint _blockCrc = 0;

        /// <summary>
        /// Indicates that the compression block is full
        /// </summary>
        public bool IsFull => this._isFull;
        private bool _isFull = false;

        /// <summary>
        /// Block numeric id for distinguishing blocks
        /// </summary>
        public int BlockId => this._blockId;
        private int _blockId;

        /// <summary>
        /// Number of bytes loaded into the block compressor
        /// </summary>
        public int LoadedBytes => this._loadedBytes;
        private int _loadedBytes = 0;
        private readonly BZip2BlockCompressor _compressor;

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="blockSizeBytes"><see cref="BZip2BlockCompressor"/> block size in bytes, also initial internal buffer list capacity</param>
        /// <param name="blockId">Block number id, used to distinguish blocks in multithreadding</param>
        public BZip2ParallelOutputDataBlock(int blockSizeBytes, int blockId)
        {
            this._buffer = new MemoryStream(blockSizeBytes + 100000);
            this._internalBitStream = new BZip2BitOutputStream(this._buffer);
            this._blockId = blockId;
            this._compressor = new BZip2BlockCompressor(this, blockSizeBytes);
        }

        public void Dispose()
        {
            this._internalBitStream.Dispose();
            this._buffer.Dispose();
        }

        /// <summary>
        /// Loads a byte into the <see cref="BZip2BlockCompressor"/>'s first RLE stage
        /// </summary>
        /// <param name="value">Byte</param>
        /// <returns>True if byte was loaded, false if byte could not be loaded because block compressor is full</returns>
        public bool LoadByte(byte value)
        {
            if (this._compressor.Write(value))
            {
                this._loadedBytes++;
                return true;
            }

            // could not load the byte, means block is full
            this._isFull = true;

            return false;
        }

        /// <summary>
        /// Loads bytes from a buffer into the <see cref="BZip2BlockCompressor"/>'s first RLE stage
        /// </summary>
        /// <param name="buff">Byte buffer</param>
        /// <param name="offset">Byte buffer offset</param>
        /// <param name="length">Number of bytes to load</param>
        /// <returns>Number of bytes actually loaded</returns>
        public int LoadBytes(byte[] buff, int offset, int length)
        {
            int count = this._compressor.Write(buff, offset, length);
            this._loadedBytes += count;
            if (count < length)
            {
                // could not load all the bytes, means block is full
                this._isFull = true;
            }
            return count;
        }

        /// <summary>
        /// Starts the actual compression
        /// </summary>
        public void CompressBytes()
        {
            this._compressor.CloseBlock();
            this._blockCrc = this._compressor.CRC;
        }

        /// <summary>
        /// Writes all the buffer data to the real <see cref="BZip2BitOutputStream"/>
        /// </summary>
        /// <param name="stream">The real bit output stream</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteToRealOutputStream(BZip2BitOutputStream stream)
        {
            this._internalBitStream.Flush();

            this._buffer.Position = 0;
            while (this._bitCount >= 8)
            {
                int b = this._buffer.ReadByte();
                stream.WriteBits(8, (uint)b);
                this._bitCount -= 8;
            }

            if (this._bitCount > 0)
            {
                int b = this._buffer.ReadByte();
                b = (b >> (8 - (int)this._bitCount));
                stream.WriteBits((int)this._bitCount, (uint)b);
                this._bitCount = 0;
            }
        }

        #region IBZip2BitOutputStream implementation

        public void WriteBoolean(bool value)
        {
            this._bitCount++;
            this._internalBitStream.WriteBoolean(value);
        }

        public void WriteUnary(int value)
        {
            while (value-- > 0)
            {
                this._bitCount++;
                this._internalBitStream.WriteBoolean(true);
            }
            this._bitCount++;
            this._internalBitStream.WriteBoolean(false);
        }

        public void WriteBits(int count, uint value)
        {
            this._bitCount += count;
            this._internalBitStream.WriteBits(count, value);
        }

        public void WriteInteger(uint value)
        {
            this._bitCount += 32;
            this._internalBitStream.WriteInteger(value);
        }

        public void Flush() => this._internalBitStream.Flush();

        #endregion
    }
}
