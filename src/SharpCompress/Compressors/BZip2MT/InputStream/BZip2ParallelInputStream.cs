// Added by drone1400, July 2025
// Location: https://github.com/drone1400/bzip2

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.InputStream
{
    /// <summary>An InputStream wrapper that decompresses BZip2 data using multiple threads</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2ParallelInputStream : Stream
    {
        #region Private fields

        // block size fields
        private readonly int _blockLevel;
        private readonly int _blockSizeBytes;

        private int _mtNextInputBlockId = 0;
        private int _mtNextOutputBlockId = 0;

        // yikes! sounds bad right?
        // if one of the worker threads, this is set to true and any further attempt to
        // write/flush/close the stream will throw an exception
        private bool _unsafeFatalException = false;
        private Exception? _lastWorkerException = null;

        // dictionary of processed blocks
        private readonly Dictionary<int, BZip2ParallelInputDataBlock> _mtDecodedBlocks = new Dictionary<int, BZip2ParallelInputDataBlock>();

        private int _mtPendingBlocks = 0;
        private readonly int _mtMaxPendingBlocks;

        private readonly int _mtWorkerBufferSize;

        private readonly object _syncRootProcesing = new object();


        // The stream from which compressed BZip2 data is read and decoded
        private Stream _inputStream;

        // True if the underlying stream will be closed with the current Stream
        private readonly bool _isOwner;

        // An InputStream wrapper that provides bit-level reads
        private BZip2BitInputStreamSplitter _inputStreamSplitter;


        // The merged CRC of all blocks decompressed so far
        private uint _streamCrc;

        #endregion

        #region Public methods

        /// <summary>Public constructor</summary>
        /// <param name="inputStream">The InputStream to wrap</param>
        /// <param name="isOwner">True if the underlying stream will be closed with the current Stream</param>
        /// <param name="inputStreamHeaderCheck"><see cref="InputStreamHeaderCheckType"/></param>
        /// <param name="manualBlockLevel">Used when <see cref="inputStreamHeaderCheck"/> is NoHeader</param>
        /// <param name="maxPendingBlocks">Maximum number of blocks to read/decompress at the same time</param>
        /// <param name="workerBufferSize">Minimum buffer size for each worker thread during decompression, in bytes.</param>
        public BZip2ParallelInputStream(Stream inputStream, bool isOwner = true, InputStreamHeaderCheckType inputStreamHeaderCheck = InputStreamHeaderCheckType.FULL_HEADER, int manualBlockLevel = 9, int maxPendingBlocks = 0, int workerBufferSize = 12582912)
        {
            if (maxPendingBlocks == 0)
            {
                maxPendingBlocks = Environment.ProcessorCount;
            }
            this._mtMaxPendingBlocks = maxPendingBlocks;
            this._mtWorkerBufferSize = workerBufferSize;

            // initialize stream
            this._inputStream = inputStream;
            this._isOwner = isOwner;
            this._inputStreamSplitter = new BZip2BitInputStreamSplitter(inputStream, inputStreamHeaderCheck, manualBlockLevel);

            this._blockLevel = this._inputStreamSplitter.BlockLevel;
            this._blockSizeBytes = this._inputStreamSplitter.BlockSizeBytes;
        }

        #endregion

        /// <summary>
        /// Decompress a block of data
        /// </summary>
        /// <param name="blockData"><see cref="BZip2ParallelInputDataBlock"/></param>
        /// <exception cref="IOException">if compressing the block somehow fails...</exception>
        private void MultiThreadWorkerAction(object? blockData)
        {
            try
            {
                if (blockData is BZip2ParallelInputDataBlock dataBlock)
                {
                    // do the work
                    dataBlock.Decompress();

                    // queue up the finished decompressed block data
                    lock (this._syncRootProcesing)
                    {
                        this._mtDecodedBlocks.Add(dataBlock.BlockId, dataBlock);
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
        /// If there are not too many pending blocks being processed, reads another block from the input stream and adds it to the work queue.
        /// </summary>
        /// <returns>True if the data block was queued up, false otherwise</returns>
        /// <exception cref="IOException">if one of the worker threads or writing to the final bit stream experienced an exception</exception>
        /// <remarks>Should only be called from within <see cref="Read"/> or <see cref="ReadByte"/></remarks>
        private bool TryToQueueUpAnotherBlock()
        {
            if (this._unsafeFatalException)
            {
                throw new IOException("One of the decompression threads somehow failed... This should never happen.", this._lastWorkerException);
            }

            if (this._inputStreamSplitter.IsStreamComplete)
            {
                return false;
            }

            lock (this._syncRootProcesing)
            {
                if (this._mtPendingBlocks >= this._mtMaxPendingBlocks)
                    return false;
            }

            // extract the next block...
            MemoryStream? ms = this._inputStreamSplitter.CopyNextBlock();

            // this should be impossible if this._inputStreamSplitter.IsStreamComplete is not true
            if (ms is null)
                return false;

            lock (this._syncRootProcesing)
            {
                this._mtPendingBlocks ++;
                BZip2ParallelInputDataBlock data = new BZip2ParallelInputDataBlock(this._mtNextInputBlockId++, ms, this._blockSizeBytes, this._mtWorkerBufferSize);
                ThreadPool.QueueUserWorkItem(this.MultiThreadWorkerAction, data);
            }

            return true;
        }

        /// <summary>
        /// Checks if we are done processing the stream. This is done by checing if there are any pending blocks to be decoded or read.
        /// Also by checking if the input stream splitter helper has reached the end of stream markers.
        /// </summary>
        /// <returns>True if we are done, false otherwise</returns>
        /// <exception cref="IOException">If there's a CRC error at end of the stream.</exception>
        /// /// <remarks>Should only be called from within <see cref="Read"/> or <see cref="ReadByte"/></remarks>
        private bool CheckIfCompletelyDone()
        {
            if (this._unsafeFatalException)
            {
                throw new IOException("One of the decompression threads somehow failed... This should never happen.", this._lastWorkerException);
            }

            lock (this._syncRootProcesing)
            {
                if (this._mtPendingBlocks == 0 &&
                    this._mtDecodedBlocks.Count == 0 &&
                    this._inputStreamSplitter.IsStreamComplete)
                {
                    if (this._inputStreamSplitter.FinalCrc != this._streamCrc)
                    {
                        throw new IOException("BZip2 stream CRC error");
                    }
                    // we are compeltely done, can no longer do anything!
                    return true;
                }
            }

            return false;
        }

        #region Implementation of abstract members of Stream

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void Flush() => throw new NotSupportedException($"{nameof(BZip2ParallelInputStream)} does not support 'Flush()' method.");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException($"{nameof(BZip2ParallelInputStream)} does not support 'Seek(long offset, SeekOrigin origin)' method.");
        public override void SetLength(long value) => throw new NotSupportedException($"{nameof(BZip2ParallelInputStream)} does not support 'SetLength(long value)' method.");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException($"{nameof(BZip2ParallelInputStream)} does not support 'Write(byte[] buffer, int offset, int count)' method.");
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => this._inputStream.Length;
        public override long Position
        {
            get => this._inputStream.Position;
            set => throw new NotSupportedException($"{nameof(BZip2ParallelInputStream)} does not support Set operation for property 'Position'.");
        }

        public override int ReadByte()
        {
            while (true)
            {
                // check if the next output block is done
                BZip2ParallelInputDataBlock? data = null;
                lock (this._syncRootProcesing)
                {
                    if (this._mtDecodedBlocks.ContainsKey(this._mtNextOutputBlockId))
                    {
                        data = this._mtDecodedBlocks[this._mtNextOutputBlockId];
                    }
                }

                if (data != null)
                {
                    if (data.IsCrcOk == false)
                    {
                        this._unsafeFatalException = true;
                        throw new IOException("BZip2 block decompression CRC error...");
                    }

                    if (data.IsDone == false)
                    {
                        // can read next byte
                        return data.Read();
                    }

                    // decompressor is done!
                    lock (this._syncRootProcesing)
                    {
                        uint blockCrc = data.CrcValue;
                        this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ blockCrc;

                        this._mtDecodedBlocks.Remove(this._mtNextOutputBlockId);
                        this._mtNextOutputBlockId ++;

                        // continue to processing next block that is already decoded, skip queueing up more blocks...
                        continue;
                    }
                }

                // queue up the next block
                bool queueSuccessful = this.TryToQueueUpAnotherBlock();

                // check if we are completely done
                if (this.CheckIfCompletelyDone())
                    return -1;

                if (!queueSuccessful)
                {
                    Thread.Sleep(1);
                }
            }
        }

        public override int Read(byte[] destination,  int offset,  int length)
        {
            int totalRead = 0;
            int remaining = length;

            while (true)
            {
                // check if the next output block is done
                BZip2ParallelInputDataBlock? data = null;
                lock (this._syncRootProcesing)
                {
                    if (this._mtDecodedBlocks.ContainsKey(this._mtNextOutputBlockId))
                    {
                        data = this._mtDecodedBlocks[this._mtNextOutputBlockId];
                    }
                }

                if (data != null)
                {
                    if (data.IsCrcOk == false)
                    {
                        this._unsafeFatalException = true;
                        throw new IOException("BZip2 block decompression CRC error...");
                    }

                    int readCount = data.Read(destination, offset, remaining);
                    remaining -= readCount;
                    offset += readCount;
                    totalRead += readCount;

                    if (totalRead >= length)
                        return totalRead;

                    if (data.IsDone)
                    {
                        // decompressor is done!
                        lock (this._syncRootProcesing)
                        {
                            uint blockCrc = data.CrcValue;
                            this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ blockCrc;

                            this._mtDecodedBlocks.Remove(this._mtNextOutputBlockId);
                            this._mtNextOutputBlockId ++;

                            // continue to processing next block that is already decoded, skip queueing up more blocks...
                            continue;
                        }
                    }
                }

                // queue up the next block
                bool queueSuccessful = this.TryToQueueUpAnotherBlock();

                // check if we are completely done
                if (this.CheckIfCompletelyDone())
                    return totalRead;

                if (!queueSuccessful)
                {
                    Thread.Sleep(1);
                }
            }
        }

        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing)
        {
            this._mtDecodedBlocks.Clear();

            if (this._isOwner)
            {
                //this._inputStream.Close();
                this._inputStream.Dispose();
            }
        }

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #endregion

    }
}
