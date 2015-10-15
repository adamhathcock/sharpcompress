namespace SharpCompress.Compressor.LZMA.Utilites
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.IO;

    internal class ReadingCrcBuilderStream : Stream
    {
        private uint mCRC;
        private bool mFinished;
        private long mProcessed;
        private Stream mSource;

        public ReadingCrcBuilderStream(Stream source)
        {
            this.mSource = source;
            this.mCRC = uint.MaxValue;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    this.mSource.Dispose();
                }
            }
            finally
            {
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
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if ((count > 0) && !this.mFinished)
            {
                int length = this.mSource.Read(buffer, offset, count);
                if (length > 0)
                {
                    this.mProcessed += length;
                    this.mCRC = CRC.Update(this.mCRC, buffer, offset, length);
                    return length;
                }
                this.Finish();
            }
            return 0;
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
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get
            {
                return this.mSource.CanRead;
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
                return false;
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

