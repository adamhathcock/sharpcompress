using System;
using System.IO;

namespace SharpCompress.Compressors.LZMA.Utilites
{
    internal class CrcBuilderStream : Stream
    {
        private readonly Stream mTarget;
        private uint mCRC;
        private bool mFinished;
        private bool isDisposed;

        public CrcBuilderStream(Stream target)
        {
            mTarget = target;
            mCRC = CRC.kInitCRC;
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            mTarget.Dispose();
            base.Dispose(disposing);
        }

        public long Processed { get; private set; }

        public uint Finish()
        {
            if (!mFinished)
            {
                mFinished = true;
                mCRC = CRC.Finish(mCRC);
            }

            return mCRC;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush()
        {
        }

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

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
            if (mFinished)
            {
                throw new InvalidOperationException("CRC calculation has been finished.");
            }

            Processed += count;
            mCRC = CRC.Update(mCRC, buffer, offset, count);
            mTarget.Write(buffer, offset, count);
        }
    }

    internal class ReadingCrcBuilderStream : Stream
    {
        private readonly Stream mSource;
        private uint mCRC;
        private bool mFinished;

        public ReadingCrcBuilderStream(Stream source)
        {
            mSource = source;
            mCRC = CRC.kInitCRC;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    mSource.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public long Processed { get; private set; }

        public uint Finish()
        {
            if (!mFinished)
            {
                mFinished = true;
                mCRC = CRC.Finish(mCRC);
            }

            return mCRC;
        }

        public override bool CanRead => mSource.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > 0 && !mFinished)
            {
                int read = mSource.Read(buffer, offset, count);
                if (read > 0)
                {
                    Processed += read;
                    mCRC = CRC.Update(mCRC, buffer, offset, read);
                    return read;
                }

                Finish();
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
    }
}