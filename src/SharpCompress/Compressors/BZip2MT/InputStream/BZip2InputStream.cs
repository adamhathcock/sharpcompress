// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using System.IO;
using SharpCompress.Compressors.BZip2MT.Algorithm;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.InputStream
{
    /// <summary>An InputStream wrapper that decompresses BZip2 data</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2InputStream : Stream
    {
        // The stream from which compressed BZip2 data is read and decoded
        private Stream _inputStream;

        // True if the underlying stream will be closed with the current Stream
        private readonly bool _isOwner;

        // An InputStream wrapper that provides bit-level reads
        private BZip2BitInputStream _bitInputStream;

        // (@code true} if the end of the compressed stream has been reached, otherwise false
        private bool _streamComplete;

        /// <summary>
        /// The declared block size of the stream (before final run-length decoding). The final block
        /// will usually be smaller, but no block in the stream has to be exactly this large, and an
        /// encoder could in theory choose to mix blocks of any size up to this value. Its function is
        /// therefore as a hint to the decompressor as to how much working space is sufficient to
        /// decompress blocks in a given stream
        /// </summary>
        private uint _streamBlockSize;

        // The merged CRC of all blocks decompressed so far
        private uint _streamCrc;

        // The decompressor for the current block
        private BZip2BlockDecompressor? _blockDecompressor;


        /// <summary>Public constructor</summary>
        /// <param name="inputStream">The InputStream to wrap</param>
        /// <param name="isOwner">if true, will close the stream when done</param>
        /// <param name="inputStreamHeaderCheck"><see cref="InputStreamHeaderCheckType"/></param>
        /// <param name="manualBlockLevel">Used when <see cref="inputStreamHeaderCheck"/> is NoHeader</param>
        public BZip2InputStream(Stream inputStream, bool isOwner = true, InputStreamHeaderCheckType inputStreamHeaderCheck = InputStreamHeaderCheckType.FULL_HEADER, int manualBlockLevel = 9)
        {
            this._inputStream = inputStream;
            this._bitInputStream = new BZip2BitInputStream(inputStream);
            this._isOwner = isOwner;

            // initialize stream immediately
            this.InitializeStream(inputStreamHeaderCheck, manualBlockLevel);
            // prepare first block
            this.InitializeNextBlock();
        }

        #region Implementation of abstract members of Stream

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void Flush() => throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Flush()' method.");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Seek(long offset, SeekOrigin origin)' method.");
        public override void SetLength(long value) => throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'SetLength(long value)' method.");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Write(byte[] buffer, int offset, int count)' method.");
        public override bool CanRead => this._inputStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => this._inputStream.Length;
        public override long Position
        {
            get => this._inputStream.Position;
            set =>throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support Set operation for property 'Position'.");
        }

        public override int ReadByte()
        {
            var nextByte = this._blockDecompressor?.Read() ?? -1;

            // if current block has reached its end, prepare next block and try reading again
            if (nextByte == -1)
            {
                if (this.InitializeNextBlock())
                {
                    nextByte = this._blockDecompressor?.Read() ?? -1;
                }
            }

            return nextByte;
        }

        public override int Read(byte[] destination,  int offset,  int length)
        {
            int bytesRead = this._blockDecompressor?.Read(destination, offset, length) ?? 0;

            // if current block has reached its end, prepare next block and try reading again
            if (bytesRead == -1)
            {
                if (this.InitializeNextBlock())
                {
                    bytesRead = this._blockDecompressor?.Read(destination, offset, length) ?? 0;
                }
            }

            if (bytesRead == -1) bytesRead = 0;

            return bytesRead;
        }

        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing)
        {
            this._streamComplete = true;
            this._blockDecompressor = null;

            this._bitInputStream.Dispose();

            if (this._isOwner)
            {
                //this._inputStream.Close();
                this._inputStream.Dispose();
            }
        }

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #endregion

        /// <summary>Reads the stream header and checks that the data appears to be a valid BZip2 stream</summary>
        /// <exception cref="IOException">if the stream header is not valid</exception>
        private void InitializeStream(InputStreamHeaderCheckType inputStreamHeaderCheck, int blockLevel)
        {
            /* If the stream has been explicitly closed, throw an exception */
            if (this._bitInputStream is null)
                throw new IOException("Stream closed");

            // If we're already at the end of the stream, do nothing
            if (this._streamComplete)
                return;

            // Read the stream header
            try
            {
                switch (inputStreamHeaderCheck)
                {
                    case InputStreamHeaderCheckType.FULL_HEADER:
                    {
                        uint marker1 = this._bitInputStream.ReadBits(16);
                        uint marker2 = this._bitInputStream.ReadBits(8);
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (marker1 != BZip2Constants.STREAM_START_MARKER_1 ||
                            marker2 !=  BZip2Constants.STREAM_START_MARKER_2 ||
                            blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                    case InputStreamHeaderCheckType.NO_BZ:
                    {
                        uint marker2 = this._bitInputStream.ReadBits(8);
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (marker2 !=  BZip2Constants.STREAM_START_MARKER_2 ||
                            blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                    case InputStreamHeaderCheckType.NO_BZH:
                    {
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                }

                this._streamBlockSize = (uint)(blockLevel * 100000);
            } catch (IOException)
            {
                // If the stream header was not valid, stop trying to read more data
                this._streamComplete = true;
                throw;
            }
        }

        /// <summary>Prepares a new block for decompression if any remain in the stream</summary>
        /// <remarks>If a previous block has completed, its CRC is checked and merged into the stream CRC.
        /// If the previous block was the final block in the stream, the stream CRC is validated</remarks>
        /// <return>true if a block was successfully initialised, or false if the end of file marker was encountered</return>
        /// <exception cref="IOException">If either the block or stream CRC check failed, if the following data is
        /// not a valid block-header or end-of-file marker, or if the following block could not be decoded</exception>
        private bool InitializeNextBlock()
        {

            // If we're already at the end of the stream, do nothing
            if (this._streamComplete)
                return false;

            // If a block is complete, check the block CRC and integrate it into the stream CRC
            if (this._blockDecompressor != null)
            {
                uint blockCrc = this._blockDecompressor.CheckCrc();
                this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ blockCrc;
            }

            // Read block-header or end-of-stream marker
            uint marker1 = this._bitInputStream.ReadBits(24);
            uint marker2 = this._bitInputStream.ReadBits(24);

            if (marker1 == BZip2Constants.BLOCK_HEADER_MARKER_1 && marker2 == BZip2Constants.BLOCK_HEADER_MARKER_2)
            {
                // Initialise a new block
                try
                {
                    this._blockDecompressor = new BZip2BlockDecompressor(this._bitInputStream, this._streamBlockSize);
                } catch (IOException)
                {
                    // If the block could not be decoded, stop trying to read more data
                    this._streamComplete = true;
                    throw;
                }
                return true;
            }
            if (marker1 == BZip2Constants.STREAM_END_MARKER_1 && marker2 == BZip2Constants.STREAM_END_MARKER_2)
            {
                // Read and verify the end-of-stream CRC
                this._streamComplete = true;
                uint storedCombinedCrc = this._bitInputStream.ReadInteger(); // .ReadBits(32);

                if (storedCombinedCrc != this._streamCrc)
                    throw new IOException("BZip2 stream CRC error");

                return false;
            }

            // If what was read is not a valid block-header or end-of-stream marker, the stream is broken
            this._streamComplete = true;
            throw new IOException("BZip2 stream format error");
        }
    }
}
