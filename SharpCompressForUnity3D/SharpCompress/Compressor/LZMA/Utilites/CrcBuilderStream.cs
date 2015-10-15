namespace SharpCompress.Compressor.LZMA.Utilites
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.IO;

    internal class CrcBuilderStream : Stream
    {
        private bool isDisposed;
        private uint mCRC;
        private bool mFinished;
        private long mProcessed;
        private Stream mTarget;

        public CrcBuilderStream(Stream target)
        {
            this.mTarget = target;
            this.mCRC = uint.MaxValue;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.mTarget.Dispose();
                base.Dispose(disposing);
            }
        }

        public uint Finish()
        {
            if (!this.mFinished)
            {
                this.mFinished = true;
                this.mCRC = CRC.Finish(this.mCRC);
            }
            return this.mCRC;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.mFinished)
            {
                throw new InvalidOperationException("CRC calculation has been finished.");
            }
            this.mProcessed += count;
            this.mCRC = CRC.Update(this.mCRC, buffer, offset, count);
            this.mTarget.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public long Processed
        {
            get
            {
                return this.mProcessed;
            }
        }
    }
}

