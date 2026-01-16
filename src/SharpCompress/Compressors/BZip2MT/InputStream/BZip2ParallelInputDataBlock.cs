using System;
using System.IO;
using SharpCompress.Compressors.BZip2MT.Algorithm;

namespace SharpCompress.Compressors.BZip2MT.InputStream
{
    internal class BZip2ParallelInputDataBlock : IDisposable
    {
        private int _blockId = 0;
        private MemoryStream? _inputBlockBuffer;
        private MemoryStream? _outputBuffer;
        private int _outputBufferSize;
        private int _blockSizeBytes;
        private bool _isCrcOk = false;
        private uint _crcValue;

        public long Length => this._outputBuffer?.Length ?? 0;
        public long Position => this._outputBuffer?.Position ?? 0;
        public bool IsDone => this.Position >= this.Length;
        public int BlockId => this._blockId;
        public bool IsCrcOk => this._isCrcOk;
        public uint CrcValue => this._crcValue;

        public BZip2ParallelInputDataBlock(
            int blockId,
            MemoryStream inputBlockBuffer,
            int blockSizeBytes,
            int outputBufferSize
        )
        {
            this._blockId = blockId;
            this._inputBlockBuffer = inputBlockBuffer;
            this._blockSizeBytes = blockSizeBytes;
            this._outputBufferSize = outputBufferSize;
        }

        public void Dispose()
        {
            this._inputBlockBuffer?.Dispose();
            this._outputBuffer?.Dispose();
        }

        /// <summary>
        /// Decompresses the full block
        /// </summary>
        public void Decompress()
        {
            if (this._inputBlockBuffer is null)
                throw new IOException("Attempted to Decompress the same block twice");

            this._outputBuffer = new MemoryStream(this._outputBufferSize);

            // note: we need to skip the first 6 magic bytes...
            this._inputBlockBuffer.Position = 6;
            using BZip2BitInputStream inputStream = new BZip2BitInputStream(this._inputBlockBuffer);
            BZip2BlockDecompressor blockDecompressor = new BZip2BlockDecompressor(
                inputStream,
                (uint)this._blockSizeBytes
            );

            int readCount = blockDecompressor.ReadAll(this._outputBuffer);

            // reset buffer position
            this._outputBuffer.Position = 0;

            try
            {
                this._crcValue = blockDecompressor.CheckCrc();
                this._isCrcOk = true;
            }
            catch (Exception)
            {
                this._crcValue = 0;
                this._isCrcOk = false;
            }

            this._inputBlockBuffer.Dispose();
            this._inputBlockBuffer = null;
        }

        /// <summary>
        /// reads a single byte from the output buffer
        /// </summary>
        /// <returns></returns>
        public int Read()
        {
            if (this._outputBuffer is null)
                throw new IOException(
                    "Attempted to read decompressed data before decompressing block"
                );

            if (this._isCrcOk == false)
                return -1;
            if (this._outputBuffer.Position < this._outputBuffer.Length)
                return this._outputBuffer.ReadByte();
            return -1;
        }

        /// <summary>
        /// Reads a number of bytes from the output buffer into the destination buffer
        /// </summary>
        /// <param name="destination">destination buffer</param>
        /// <param name="offset">starting position within destination buffer</param>
        /// <param name="length">maximum number of bytes to read</param>
        /// <returns></returns>
        public int Read(byte[] destination, int offset, int length)
        {
            if (this._outputBuffer is null)
                throw new IOException(
                    "Attempted to read decompressed data before decompressing block"
                );

            if (this._isCrcOk == false)
                return 0;

            return this._outputBuffer.Read(destination, offset, length);
        }
    }
}
