// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using System.IO;
using SharpCompress.Compressors.BZip2MT.Algorithm;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.OutputStream
{
    /// <summary>An OutputStream wrapper that compresses BZip2 data</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2OutputStream : Stream
    {
        // The stream to which compressed BZip2 data is written
        private Stream _outputStream;

        // An OutputStream wrapper that provides bit-level writes
        private readonly BZip2BitOutputStream _bitOutputStream;

        // (@code true} if the compressed stream has been finished, otherwise false
        private bool _streamFinished;

        // The declared maximum block size of the stream (before final run-length decoding)
        private readonly int _streamBlockSize;

        // The merged CRC of all blocks compressed so far
        private uint _streamCrc;

        // The compressor for the current block
        private BZip2BlockCompressor? _blockCompressor;

        // True if the underlying stream will be closed with the current Stream
        private bool _isOwner;

        /// <summary>Public constructor</summary>
        /// <param name="outputStream">The output stream to write to</param>
        /// <param name="blockSizeMultiplier">The BZip2 block size as a multiple of 100,000 bytes (minimum 1, maximum 9)</param>
        /// <param name="isOwner">True if the underlying stream will be closed with the current Stream</param>
        /// <exception>On any I/O error writing to the output stream</exception>
        /// <remarks>Larger block sizes require more memory for both compression and decompression,
        /// but give better compression ratios. 9 will usually be the best value to use</remarks>
        public BZip2OutputStream(Stream outputStream, bool isOwner = true, int blockSizeMultiplier = 9)
        {
            if (outputStream is null)
                throw new ArgumentException("Null output stream");

            if ((blockSizeMultiplier < 1) || (blockSizeMultiplier > 9))
                throw new ArgumentException("Invalid BZip2 block size" + blockSizeMultiplier);

            this._streamBlockSize = blockSizeMultiplier * 100000;
            this._outputStream = outputStream;
            this._bitOutputStream = new BZip2BitOutputStream(this._outputStream);
            this._isOwner = isOwner;

            this._bitOutputStream.WriteBits(16, BZip2Constants.STREAM_START_MARKER_1);
            this._bitOutputStream.WriteBits(8, BZip2Constants.STREAM_START_MARKER_2);
            this._bitOutputStream.WriteBits(8, (uint)('0' + blockSizeMultiplier));

            this.InitialiseNextBlock();
        }

        #region Implementation of abstract members of Stream

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void Flush () => throw new NotSupportedException($"{nameof(BZip2OutputStream)} does not support 'Flush()' method.");

        public override int Read (byte[] buffer, int offset, int count) => throw new NotSupportedException($"{nameof(BZip2OutputStream)} does not support 'Read(byte[] buffer, int offset, int count)' method.");

        public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException($"{nameof(BZip2OutputStream)} does not support 'Seek(long offset, SeekOrigin origin)' method.");

        public override void SetLength (long value) => throw new NotSupportedException($"{nameof(BZip2OutputStream)} does not support 'SetLength(long value)' method.");

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => this._outputStream.CanWrite;

        public override long Length => this._outputStream.Length;

        public override long Position
        {
            get => this._outputStream.Position;
            set =>throw new NotSupportedException($"{nameof(BZip2OutputStream)} does not support Set operation for property 'Position'.");
        }

        public override void WriteByte(byte value)
        {
            if (this._outputStream is null)
                throw new IOException("Stream closed");

            if (this._blockCompressor is null || this._streamFinished)
                throw new IOException("Write beyond end of stream");

            if (!this._blockCompressor.Write(value & 0xff))
            {
                this.CloseBlock();
                this.InitialiseNextBlock();
                this._blockCompressor.Write(value & 0xff);
            }
        }

        public override void Write(byte[] data, int offset, int length)
        {
            if (this._outputStream is null)
                throw new IOException("Stream closed");

            if (this._blockCompressor is null || this._streamFinished)
                throw new IOException("Write beyond end of stream");

            while (length > 0)
            {
                int bytesWritten;
                if ((bytesWritten = this._blockCompressor.Write(data, offset, length)) < length)
                {
                    this.CloseBlock();
                    this.InitialiseNextBlock();
                }
                offset += bytesWritten;
                length -= bytesWritten;
            }
        }

        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing)
        {
            this.Finish();
            if (this._isOwner)
            {
                this._outputStream.Dispose();
            }
        }

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #endregion

        /// <summary>Initialises a new block for compression</summary>
        private void InitialiseNextBlock() => this._blockCompressor = new BZip2BlockCompressor (this._bitOutputStream, this._streamBlockSize);

        /// <summary>Compress and write out the block currently in progress</summary>
        /// <remarks>If no bytes have been written to the block, it is discarded</remarks>
        /// <exception>On any I/O error writing to the output stream</exception>
        private void CloseBlock()
        {
            if (this._blockCompressor is null || this._blockCompressor.IsEmpty)
                return;

            this._blockCompressor.CloseBlock();
            this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ this._blockCompressor.CRC;
        }

        /// <summary>Compresses and writes out any as yet unwritten data, then writes the end of the BZip2 stream</summary>
        /// <remarks>The underlying OutputStream is not closed</remarks>
        /// <exception>On any I/O error writing to the output stream</exception>
        private void Finish()
        {
            if (!this._streamFinished)
            {
                this._streamFinished = true;
                try
                {
                    this.CloseBlock();
                    this._bitOutputStream.WriteBits(24, BZip2Constants.STREAM_END_MARKER_1);
                    this._bitOutputStream.WriteBits(24, BZip2Constants.STREAM_END_MARKER_2);
                    this._bitOutputStream.WriteInteger(this._streamCrc);
                    this._bitOutputStream.Flush();
                    this._outputStream.Flush();
                } finally
                {
                    this._blockCompressor = null;
                }
            }
        }
    }
}
