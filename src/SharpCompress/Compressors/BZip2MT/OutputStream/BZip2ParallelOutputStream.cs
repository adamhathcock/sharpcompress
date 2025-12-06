// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace SharpCompress.Compressors.BZip2MT.OutputStream
{

    /// <summary>An OutputStream wrapper that compresses BZip2 data using multiple threads</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2ParallelOutputStream : Stream
    {
        // block size fields
        private readonly int _compressBlockSize;
        private readonly int _blockLevel;

        private int _mtNextInputBlockId = 0;
        private int _mtNextOutputBlockId = 0;

        // flag indicating input stream is finished
        private bool _mtStreamIsFinished = false;

        // yikes! sounds bad right?
        // if one of the worker threads, this is set to true and any furhter attempt to
        // write/flush/close the stream will throw an exception
        private bool _unsafeFatalException = false;
        private Exception? _lastWorkerException = null;

        // dictionary of processed blocks
        private readonly Dictionary<int, BZip2ParallelOutputDataBlock> _mtProcessedBlocks = new Dictionary<int, BZip2ParallelOutputDataBlock>();

        private int _mtPendingBlocks = 0;

        private readonly object _syncRootProcesing = new object();
        private readonly object _syncRootOutputStream = new object();

        // The output stream
        private readonly Stream _outputStream;

        // The bit output stream
        private readonly BZip2BitOutputStream _bitStream;

        // The merged CRC of all blocks compressed so far
        private uint _streamCrc = 0;

        //
        private BZip2ParallelOutputDataBlock _currentBlockBuffer;

        // True if the underlying stream will be closed with the current Stream
        private readonly bool _isOwner;

        // for debug purposes...
        // private int _debugMaxMetaBufferDataPairCount = 0;

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="output">The output stream in which to write the compressed data</param>
        /// <param name="isOwner">True if the underlying stream will be closed with the current Stream</param>
        /// <param name="blockLevel">The BZip2 block size as a multiple of 100,000 bytes (minimum 1, maximum 9)</param>
        /// <remarks>For best performance, compressor thread number should be equal to CPU core count, but results may vary</remarks>
        public BZip2ParallelOutputStream(Stream output, bool isOwner = true, int blockLevel = 9)
        {
            this._outputStream = output;
            this._bitStream = new BZip2BitOutputStream(output);

            if (blockLevel < 1 ) blockLevel = 1;
            if (blockLevel > 9 ) blockLevel = 9;
            this._blockLevel = blockLevel;

            // evaluate thread count
            // if (compressorThreads < 1) compressorThreads = 1;
            // if (compressorThreads > ABSOLUTE_MAX_THREADS) compressorThreads = ABSOLUTE_MAX_THREADS;
            // this._mtCompressorThreads = compressorThreads;

            // supposedly a block can only expand 1.25x, so 0.8 of normal block size should always be safe...
            this._compressBlockSize = 100000 * this._blockLevel;

            // initialize initial meta buffer
            this._currentBlockBuffer = new BZip2ParallelOutputDataBlock(this._compressBlockSize, this._mtNextInputBlockId++);

            this._isOwner = isOwner;

            // write the bz2 header...
            this.WriteBz2Header();
        }

        /// <summary>
        /// Writes the next pending Output data block to the output bit stream
        /// </summary>
        /// <returns>True if a block was written, false if didn't find any pending blocks</returns>
        /// <exception cref="IOException"></exception>
        private bool TryWriteOutputBlockAndIncrementId()
        {
            if (this._unsafeFatalException)
            {
                throw new IOException("One of the compression threads somehow failed... This should never happen.", this._lastWorkerException);
            }


            try
            {
                BZip2ParallelOutputDataBlock? currentOutput = null;

                lock (this._syncRootProcesing)
                {
                    // check if the next output block can be extracted
                    if (this._mtProcessedBlocks.ContainsKey(this._mtNextOutputBlockId))
                    {
                        currentOutput = this._mtProcessedBlocks[this._mtNextOutputBlockId];
                        this._mtProcessedBlocks.Remove(this._mtNextOutputBlockId);
                    }

                    // check if we got anything to write
                    if (currentOutput is null)
                        return false;

                    this._mtNextOutputBlockId++;
                }

                lock (this._syncRootOutputStream)
                {
                    // update file CRC
                    this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ currentOutput.BlockCrc;
                    currentOutput.WriteToRealOutputStream(this._bitStream);
                }
                return true;
            } catch (Exception ex)
            {
                // set this without any locks...
                this._lastWorkerException = ex;
                this._unsafeFatalException = true;

                // rethrow exception, hopefully something catches it?...
                throw new IOException("BZip2 error writing output data! See inner exception for details!", ex);
            }
        }

        /// <summary>
        /// Compresses a block of data
        /// </summary>
        /// <param name="blockData"><see cref="BZip2ParallelOutputDataBlock"/></param>
        /// <exception cref="IOException">if compressing the block somehow fails...</exception>
        private void MultiThreadWorkerAction(object? blockData)
        {
            try
            {
                if (blockData is BZip2ParallelOutputDataBlock buffer)
                {
                    // do the work
                    buffer.CompressBytes();

                    // queue up the finished compressed block data
                    lock (this._syncRootProcesing)
                    {
                        this._mtProcessedBlocks.Add(buffer.BlockId, buffer);
                    }
                }
            } catch (Exception ex)
            {
                // set this without any locks...
                this._lastWorkerException = ex;
                this._unsafeFatalException = true;

                throw new IOException("BZip2 Processing thread somehow crashed... See inner exception for details!", ex);
            } finally
            {
                lock (this._syncRootProcesing)
                {
                    this._mtPendingBlocks --;
                }
            }
        }

        /// <summary>
        /// Writes the BZ2 header to the bit stream
        /// </summary>
        private void WriteBz2Header()
        {
            lock (this._syncRootOutputStream)
            {
                // write BZIP file header
                this._bitStream.WriteBits(8, 0x42);                            // B
                this._bitStream.WriteBits(8, 0x5A);                            // Z
                this._bitStream.WriteBits(8, 0x68);                            // h
                this._bitStream.WriteBits(8, (uint)(0x30 + this._blockLevel)); // block level digit
            }
        }

        /// <summary>
        /// Writes the BZ2 footer tot he bit stream and flushes the stream
        /// </summary>
        private void WriteBz2FooterAndFlush()
        {
            lock (this._syncRootOutputStream)
            {
                // end magic
                this._bitStream.WriteBits(8, 0x17);
                this._bitStream.WriteBits(8, 0x72);
                this._bitStream.WriteBits(8, 0x45);
                this._bitStream.WriteBits(8, 0x38);
                this._bitStream.WriteBits(8, 0x50);
                this._bitStream.WriteBits(8, 0x90);

                // write combined CRC
                this._bitStream.WriteBits(16, (this._streamCrc >> 16) & 0xFFFF);
                this._bitStream.WriteBits(16, this._streamCrc & 0xFFFF);

                // flush all remaining bits
                this._bitStream.Flush();
                this._outputStream.Flush();
            }
        }

        /// <summary>
        /// Queues up the current data block if it is not empty
        /// </summary>
        /// <returns>True if the data block was queued up, false if the current block is empty</returns>
        /// <exception cref="IOException">if one of the worker threads or writing to the final bit stream experienced an exception</exception>
        private bool TryEnqueueCurrentBlockBuffer()
        {
            if (this._unsafeFatalException)
            {
                throw new IOException("One of the compression threads somehow failed... This should never happen.", this._lastWorkerException);
            }

            // make sure current block has data
            if (this._currentBlockBuffer.LoadedBytes <= 0)
                return false;

            lock (this._syncRootProcesing)
            {
                this._mtPendingBlocks ++;
                ThreadPool.QueueUserWorkItem(this.MultiThreadWorkerAction, this._currentBlockBuffer);
                this._currentBlockBuffer = new BZip2ParallelOutputDataBlock(this._compressBlockSize, this._mtNextInputBlockId++);
            }

            return true;
        }


        /// <summary>
        /// Waits for any pending data blocks to be written to the bit stream and writes the BZ2 footer
        /// </summary>
        /// <exception cref="IOException">If there was an exception in a worker thread or when writing to the final bitstream</exception>
        private void FinishBitstream()
        {
            if (this._unsafeFatalException)
            {
                throw new IOException("One of the compression threads somehow failed... This should never happen.", this._lastWorkerException);
            }

            lock (this._syncRootProcesing)
            {
                if (this._mtStreamIsFinished)
                    return;
                this._mtStreamIsFinished = true;
            }

            // check if there is still data left to write
            this.TryEnqueueCurrentBlockBuffer();

            // decrement next input block since no longer getting another block
            this._mtNextInputBlockId--;

            while (true)
            {
                if (this._unsafeFatalException)
                {
                    throw new IOException("One of the compression threads somehow failed... This should never happen.", this._lastWorkerException);
                }

                lock (this._syncRootProcesing)
                {
                    if (this._mtNextInputBlockId == this._mtNextOutputBlockId)
                    {
                        // all done, can safely exit
                        break;
                    }
                }

                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId())
                { }
                Thread.Sleep(1);
            }

            // sanity check, this should be impossible and should be caught by some test right?...
            lock (this._syncRootProcesing)
            {
                if (this._mtPendingBlocks != 0 ||
                    this._mtProcessedBlocks.Count > 0)
                {
                    throw new IOException("BZip2 dispose operation sanity check failed!...");
                }
            }

            // finally, write the footer!
            this.WriteBz2FooterAndFlush();

            //Console.WriteLine($"Max number of data pair entries was {this._debugMaxMetaBufferDataPairCount}");
        }

        #region Implementation of abstract members of Stream

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing)
        {
            this.FinishBitstream();
            base.Dispose(disposing);
            if (this._isOwner)
            {
                this._outputStream.Close();
            }
        }

        public override void Flush()
        {
            // try writing output block and keep doing so while successful
            while (this.TryWriteOutputBlockAndIncrementId())
            { }
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException($"{nameof(BZip2ParallelOutputStream)} does not support 'Seek(long offset, SeekOrigin origin)' method.");
        public override void SetLength(long value) => throw new NotSupportedException($"{nameof(BZip2ParallelOutputStream)} does not support 'SetLength(long value)' method.");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException($"{nameof(BZip2ParallelOutputStream)} does not support 'Read(byte[] buffer, int offset, int count)' method.");

        public override void WriteByte(byte value)
        {
            if (!this._currentBlockBuffer.LoadByte(value))
            {
                // byte could not be loaded, this happens when current block buffer is full

                this.TryEnqueueCurrentBlockBuffer();
                this._currentBlockBuffer.LoadByte(value);

                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId()) { }
            }
        }

        public override void Write(byte[] data, int offset, int length)
        {
            while (length > 0)
            {
                if (!this._currentBlockBuffer.IsFull)
                {
                    int count = this._currentBlockBuffer.LoadBytes(data, offset, length);
                    offset += count;
                    length -= count;
                }

                if (this._currentBlockBuffer.IsFull)
                {
                    this.TryEnqueueCurrentBlockBuffer();
                }

                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId()) { }
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => this._outputStream.CanWrite;
        public override long Length => this._outputStream.Length;

        public override long Position
        {
            get => this._outputStream.Position;
            set => throw new NotSupportedException($"{nameof(BZip2ParallelOutputStream)} does not support Set operation for property 'Position'.");
        }


        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #endregion
    }
}
